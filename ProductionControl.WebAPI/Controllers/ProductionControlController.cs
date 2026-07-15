using ProductionControl.Domain.Production;
using ProductionControl.Persistence.Context;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ProductionControl.WebAPI.Controllers;

[ApiController]
[Route("api/production")]
public class ProductionControlController : ApiControllerBase
{
    private readonly ProductionControlDbContext _db;

    public ProductionControlController(ProductionControlDbContext db) => _db = db;

    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard([FromQuery] DateTime? date)
    {
        var selectedDate = (date ?? DateTime.Today).Date;
        var nextDate = selectedDate.AddDays(1);
        var orders = await WorkOrderQuery()
            .Where(x => x.CuttingList!.PlanDate >= selectedDate && x.CuttingList.PlanDate < nextDate)
            .OrderBy(x => x.LineCode)
            .ThenBy(x => x.OrderNumber)
            .ToListAsync();

        var completedOrders = orders.Where(x => x.CompletedAt.HasValue).ToList();
        var actualQty = completedOrders.Sum(x => x.ActualQty);
        var dailyShiftOutputs = await GetDailyShiftOutputs(selectedDate);

        return ApiOk(new ProductionDashboardSummary
        {
            TotalWorkOrders = orders.Count,
            WaitingWorkOrders = orders.Count(x => x.Status == ProductionWorkOrderStatus.WAITING),
            RunningWorkOrders = orders.Count(x => x.Status == ProductionWorkOrderStatus.IN_PROGRESS),
            CompletedWorkOrders = orders.Count(x => x.Status == ProductionWorkOrderStatus.FINISH),
            ActualQty = actualQty,
            RejectQty = completedOrders.Sum(x => x.RejectQty),
            WorkOrders = orders.Select(ToResponse).ToList(),
            DailyShiftOutputs = dailyShiftOutputs
        });
    }

    [HttpGet("work-orders")]
    public async Task<IActionResult> WorkOrders([FromQuery] ProductionWorkOrderStatus? status, [FromQuery] DateTime? date)
    {
        var query = WorkOrderQuery();
        if (status.HasValue)
        {
            var statusMasterId = ProductionStatusMaster.ToId(status.Value);
            query = query.Where(x => x.StatusMasterId == statusMasterId);
        }

        if (date.HasValue)
        {
            var selectedDate = date.Value.Date;
            var nextDate = selectedDate.AddDays(1);
            query = query.Where(x => x.CuttingList!.PlanDate >= selectedDate && x.CuttingList.PlanDate < nextDate);
        }

        var items = await query.OrderByDescending(x => x.UpdatedAt).ToListAsync();
        return ApiOk(items.Select(ToResponse).ToList());
    }

    [HttpPost("work-orders")]
    public async Task<IActionResult> CreateWorkOrder([FromBody] CreateProductionWorkOrderRequest request)
    {
        try
        {
            var cuttingList = await _db.CuttingLists.FindAsync(request.CuttingListId);
            if (cuttingList is null)
            {
                return ApiNotFound("Cutting list was not found.");
            }

            if (string.IsNullOrWhiteSpace(request.OrderNumber))
            {
                throw new ArgumentException("Order number is required.");
            }

            if (await _db.ProductionWorkOrders.AnyAsync(x => x.OrderNumber == request.OrderNumber.Trim()))
            {
                throw new InvalidOperationException("Order number is already used.");
            }

            if (await _db.ProductionWorkOrders.AnyAsync(x => x.CuttingListId == cuttingList.Id))
            {
                throw new InvalidOperationException("This cutting list already has a work order.");
            }

            var order = new ProductionWorkOrder
            {
                OrderNumber = request.OrderNumber.Trim(),
                CuttingListId = cuttingList.Id,
                LineCode = string.IsNullOrWhiteSpace(request.LineCode) ? cuttingList.LineCode : request.LineCode.Trim(),
                TargetQty = 0,
                Status = ProductionWorkOrderStatus.WAITING
            };
            _db.ProductionWorkOrders.Add(order);
            await _db.SaveChangesAsync();
            await ReloadWorkOrder(order);
            return ApiCreated(ToResponse(order), "Work order created successfully.");
        }
        catch (Exception ex)
        {
            return ApiBadRequest(ex);
        }
    }

    [HttpPost("work-orders/scan")]
    public async Task<IActionResult> ScanWorkOrder([FromBody] ScanOrderNumberRequest request)
    {
        try
        {
            var code = request.OrderNumber.Trim();
            if (string.IsNullOrWhiteSpace(code))
            {
                throw new ArgumentException("Order number is required.");
            }

            var existingOrder = await _db.ProductionWorkOrders
                .Include(x => x.CuttingList)
                .Include(x => x.PicCard)
                .Include(x => x.Operators)
                    .ThenInclude(x => x.PicCard)
                .FirstOrDefaultAsync(x => x.OrderNumber == code || x.CuttingList!.CuttingListNo == code);

            if (existingOrder is not null)
            {
                AddLog(existingOrder.Id, existingOrder.PicCardId, ProductionActivityType.CUTTING_LIST_SCAN, $"Order number scan {code}");
                await _db.SaveChangesAsync();
                return ApiOk(ToResponse(existingOrder), "Order number found.");
            }

            var cuttingList = await _db.CuttingLists.FirstOrDefaultAsync(x => x.CuttingListNo == code);
            if (cuttingList is null)
            {
                return ApiNotFound("Order number / cutting list was not found.");
            }

            var order = new ProductionWorkOrder
            {
                OrderNumber = cuttingList.CuttingListNo,
                CuttingListId = cuttingList.Id,
                LineCode = cuttingList.LineCode,
                TargetQty = 0,
                Status = ProductionWorkOrderStatus.WAITING
            };

            _db.ProductionWorkOrders.Add(order);
            await _db.SaveChangesAsync();
            await ReloadWorkOrder(order);
            AddLog(order.Id, null, ProductionActivityType.CUTTING_LIST_SCAN, $"Order number scan {code}");
            await _db.SaveChangesAsync();

            return ApiCreated(ToResponse(order), "Work order created from the cutting list.");
        }
        catch (Exception ex)
        {
            return ApiBadRequest(ex);
        }
    }

    [HttpPost("work-orders/{id:int}/scan-pic")]
    public async Task<IActionResult> ScanPic(int id, [FromBody] ScanPicRequest request)
    {
        try
        {
            var order = await FindWorkOrder(id);
            if (order is null)
            {
                return ApiNotFound("Work order was not found.");
            }

            var cardUid = request.CardUid.Trim();
            var pic = await _db.PicCards.FirstOrDefaultAsync(x => x.CardUid == cardUid && x.IsActive);
            if (pic is null)
            {
                return ApiNotFound("PIC is not registered or the card is inactive.");
            }

            if (order.Status == ProductionWorkOrderStatus.FINISH)
            {
                throw new InvalidOperationException("Operators cannot be added to a finished work order.");
            }

            var activeOperator = order.Operators.FirstOrDefault(x => x.IsActive && x.PicCardId == pic.Id);
            if (activeOperator is null)
            {
                activeOperator = new ProductionWorkOrderOperator
                {
                    ProductionWorkOrderId = order.Id,
                    PicCardId = pic.Id,
                    IsActive = true,
                    ScannedAt = DateTime.Now
                };
                activeOperator.PicCard = pic;
                order.Operators.Add(activeOperator);
                _db.ProductionWorkOrderOperators.Add(activeOperator);
            }
            else
            {
                activeOperator.ScannedAt = DateTime.Now;
            }

            order.PicCardId ??= pic.Id;
            order.UpdatedAt = DateTime.Now;
            pic.LastScannedAt = DateTime.Now;
            AddLog(order.Id, pic.Id, ProductionActivityType.PIC_SCAN, $"Operator {pic.EmployeeNo} - {pic.FullName}");
            await _db.SaveChangesAsync();
            await ReloadWorkOrder(order);
            return ApiOk(ToResponse(order), "Operator added successfully.");
        }
        catch (Exception ex)
        {
            return ApiBadRequest(ex);
        }
    }

    [HttpPost("work-orders/{id:int}/operators/{operatorId:long}/remove")]
    public async Task<IActionResult> RemoveOperator(int id, long operatorId)
    {
        try
        {
            var order = await FindWorkOrder(id);
            if (order is null)
            {
                return ApiNotFound("Work order was not found.");
            }

            if (order.Status == ProductionWorkOrderStatus.FINISH)
            {
                throw new InvalidOperationException("Operators cannot be removed from a finished work order.");
            }

            var activeOperator = order.Operators.FirstOrDefault(x => x.Id == operatorId && x.IsActive);
            if (activeOperator is null)
            {
                return ApiNotFound("Active operator was not found on this work order.");
            }

            activeOperator.IsActive = false;
            activeOperator.RemovedAt = DateTime.Now;
            order.UpdatedAt = DateTime.Now;

            var nextPrimaryOperator = order.Operators
                .Where(x => x.IsActive && x.Id != operatorId)
                .OrderBy(x => x.ScannedAt)
                .FirstOrDefault();
            order.PicCardId = nextPrimaryOperator?.PicCardId;

            AddLog(order.Id, activeOperator.PicCardId, ProductionActivityType.OPERATOR_REMOVE, $"Operator {activeOperator.PicCard?.EmployeeNo} - {activeOperator.PicCard?.FullName} removed");
            await _db.SaveChangesAsync();
            await ReloadWorkOrder(order);
            return ApiOk(ToResponse(order), "Operator removed successfully.");
        }
        catch (Exception ex)
        {
            return ApiBadRequest(ex);
        }
    }

    [HttpPost("work-orders/{id:int}/start")]
    public async Task<IActionResult> Start(int id, [FromBody] StartWorkOrderRequest? request)
    {
        try
        {
            var order = await FindWorkOrder(id);
            if (order is null)
            {
                return ApiNotFound("Work order was not found.");
            }

            if (order.Status == ProductionWorkOrderStatus.FINISH)
            {
                throw new InvalidOperationException("A finished work order cannot be started.");
            }

            if (order.Status == ProductionWorkOrderStatus.IN_PROGRESS)
            {
                throw new InvalidOperationException("Work order is already in progress.");
            }

            var activeOperators = await _db.ProductionActiveOperators
                .Include(x => x.PicCard)
                .Include(x => x.ShiftMaster)
                .Where(x => x.IsActive)
                .OrderBy(x => x.ScannedAt)
                .ToListAsync();
            if (activeOperators.Count == 0)
            {
                throw new InvalidOperationException("Scan at least one active operator before starting the work order.");
            }

            var currentShift = await GetCurrentShift();
            var areaMasterId = request?.AreaMasterId ?? order.AreaMasterId;
            if (!areaMasterId.HasValue)
            {
                throw new InvalidOperationException("Pilih area sebelum Start Order Number.");
            }

            var area = await _db.AreaMasters.FirstOrDefaultAsync(x => x.Id == areaMasterId.Value && x.IsActive);
            if (area is null)
            {
                throw new InvalidOperationException("Area tidak ditemukan atau sudah tidak aktif.");
            }

            var startedAt = order.StartedAt ?? DateTime.Now;
            SyncActiveOperatorsToWorkOrder(order, activeOperators, currentShift);

            order.Status = ProductionWorkOrderStatus.IN_PROGRESS;
            order.StartedAt = startedAt;
            order.ShiftMasterId = currentShift?.Id;
            order.AreaMasterId = area.Id;
            order.AreaMaster = area;
            order.UpdatedAt = DateTime.Now;
            order.CuttingList!.Status = CuttingListStatus.IN_PROGRESS;
            await ReplaceOperatorSnapshots(
                order.Id,
                ProductionOperatorSnapshotType.START,
                CreateSnapshotsFromActiveOperators(order.Id, ProductionOperatorSnapshotType.START, activeOperators, currentShift, startedAt));
            AddLog(order.Id, order.PicCardId, ProductionActivityType.WORK_START, $"Production work started at {area.AreaName}");
            await _db.SaveChangesAsync();
            await ReloadWorkOrder(order);
            return ApiOk(ToResponse(order), "Work order started.");
        }
        catch (Exception ex)
        {
            return ApiBadRequest(ex);
        }
    }

    [HttpPost("work-orders/{id:int}/progress")]
    public async Task<IActionResult> UpdateProgress(int id, [FromBody] UpdateProductionRequest request)
    {
        try
        {
            var order = await FindWorkOrder(id);
            if (order is null)
            {
                return ApiNotFound("Work order was not found.");
            }

            if (order.Status != ProductionWorkOrderStatus.IN_PROGRESS)
            {
                throw new InvalidOperationException("Output can only be updated while the work order is running.");
            }

            var actualQty = request.ActualQty.GetValueOrDefault(order.ActualQty);
            var rejectQty = request.RejectQty.GetValueOrDefault(0);

            if (actualQty < 0 || rejectQty < 0)
            {
                throw new ArgumentException("Quantity cannot be negative.");
            }

            order.ActualQty = actualQty;
            order.RejectQty = rejectQty;
            order.UpdatedAt = DateTime.Now;
            AddLog(order.Id, order.PicCardId, ProductionActivityType.PRODUCTION_UPDATE, request.Remarks ?? $"Actual {actualQty}, reject {rejectQty}");
            await _db.SaveChangesAsync();
            return ApiOk(ToResponse(order), "Production output updated.");
        }
        catch (Exception ex)
        {
            return ApiBadRequest(ex);
        }
    }

    [HttpPost("work-orders/{id:int}/complete")]
    public async Task<IActionResult> Complete(int id, [FromBody] UpdateProductionRequest? request)
    {
        try
        {
            var order = await FindWorkOrder(id);
            if (order is null)
            {
                return ApiNotFound("Work order was not found.");
            }

            if (order.Status != ProductionWorkOrderStatus.IN_PROGRESS)
            {
                throw new InvalidOperationException("Work order must be IN_PROGRESS before it can be completed.");
            }

            if (request is not null)
            {
                var actualQty = request.ActualQty.GetValueOrDefault(order.ActualQty);
                var rejectQty = request.RejectQty.GetValueOrDefault(0);
                if (actualQty < 0 || rejectQty < 0)
                {
                    throw new ArgumentException("Quantity cannot be negative.");
                }

                order.ActualQty = actualQty;
                order.RejectQty = rejectQty;
            }

            var completedAt = DateTime.Now;
            order.Status = ProductionWorkOrderStatus.FINISH;
            order.CompletedAt = completedAt;
            order.UpdatedAt = DateTime.Now;
            order.CuttingList!.Status = CuttingListStatus.FINISH;
            await SaveFinishOperatorSnapshots(order, completedAt);
            AddLog(order.Id, order.PicCardId, ProductionActivityType.WORK_COMPLETE, request?.Remarks ?? "Production work completed");
            await _db.SaveChangesAsync();
            return ApiOk(ToResponse(order), "Work order completed.");
        }
        catch (Exception ex)
        {
            return ApiBadRequest(ex);
        }
    }

    [HttpPost("work-orders/{id:int}/finish")]
    public async Task<IActionResult> Finish(int id)
    {
        try
        {
            var order = await FindWorkOrder(id);
            if (order is null)
            {
                return ApiNotFound("Work order was not found.");
            }

            if (order.Status != ProductionWorkOrderStatus.IN_PROGRESS)
            {
                throw new InvalidOperationException("Work order must be IN_PROGRESS before finish.");
            }

            var completedAt = DateTime.Now;
            order.Status = ProductionWorkOrderStatus.FINISH;
            order.CompletedAt = completedAt;
            order.UpdatedAt = DateTime.Now;
            order.CuttingList!.Status = CuttingListStatus.FINISH;
            await SaveFinishOperatorSnapshots(order, completedAt);
            AddLog(order.Id, order.PicCardId, ProductionActivityType.WORK_COMPLETE, "Production work finished");
            await _db.SaveChangesAsync();
            return ApiOk(ToResponse(order), "Work order finished.");
        }
        catch (Exception ex)
        {
            return ApiBadRequest(ex);
        }
    }

    [HttpPost("work-orders/{id:int}/cancel-finish")]
    public async Task<IActionResult> CancelFinish(int id)
    {
        try
        {
            var order = await FindWorkOrder(id);
            if (order is null)
            {
                return ApiNotFound("Work order was not found.");
            }

            if (order.Status != ProductionWorkOrderStatus.FINISH || !order.CompletedAt.HasValue)
            {
                throw new InvalidOperationException("Work order is not finished or can no longer be canceled.");
            }

            order.Status = ProductionWorkOrderStatus.IN_PROGRESS;
            order.CompletedAt = null;
            order.UpdatedAt = DateTime.Now;
            order.CuttingList!.Status = CuttingListStatus.IN_PROGRESS;
            await ClearOperatorSnapshots(order.Id, ProductionOperatorSnapshotType.FINISH);
            AddLog(order.Id, order.PicCardId, ProductionActivityType.WORK_RESUME, "Production finish cancelled");
            await _db.SaveChangesAsync();
            return ApiOk(ToResponse(order), "Finish canceled.");
        }
        catch (Exception ex)
        {
            return ApiBadRequest(ex);
        }
    }

    [HttpGet("cutting-lists")]
    public async Task<IActionResult> CuttingLists([FromQuery] DateTime? date)
    {
        var query = _db.CuttingLists.AsNoTracking();
        if (date.HasValue)
        {
            var selectedDate = date.Value.Date;
            var nextDate = selectedDate.AddDays(1);
            query = query.Where(x => x.PlanDate >= selectedDate && x.PlanDate < nextDate);
        }

        var cuttingLists = await query.OrderByDescending(x => x.PlanDate).ThenBy(x => x.LineCode).ToListAsync();
        var cuttingListIds = cuttingLists.Select(x => x.Id).ToList();
        List<ProductionWorkOrder> workOrders = cuttingListIds.Count == 0
            ? []
            : await WorkOrderWithSnapshotsQuery()
                .Where(x => cuttingListIds.Contains(x.CuttingListId))
                .ToListAsync();
        var workOrderByCuttingList = workOrders
            .GroupBy(x => x.CuttingListId)
            .ToDictionary(x => x.Key, x => x.OrderByDescending(order => order.UpdatedAt).First());

        return ApiOk(cuttingLists
            .Select(x => ToCuttingListResponse(x, workOrderByCuttingList.GetValueOrDefault(x.Id)))
            .ToList());
    }

    [HttpPost("cutting-lists")]
    public async Task<IActionResult> CreateCuttingList([FromBody] CreateCuttingListRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.CuttingListNo) ||
                string.IsNullOrWhiteSpace(request.ProductCode) ||
                string.IsNullOrWhiteSpace(request.ProductName) ||
                string.IsNullOrWhiteSpace(request.LineCode))
            {
                throw new ArgumentException("Cutting list number, product, and line are required.");
            }

            if (await _db.CuttingLists.AnyAsync(x => x.CuttingListNo == request.CuttingListNo.Trim()))
            {
                throw new InvalidOperationException("Cutting list number is already used.");
            }

            var item = new CuttingList
            {
                CuttingListNo = request.CuttingListNo.Trim(),
                ProductCode = request.ProductCode.Trim(),
                ProductName = request.ProductName.Trim(),
                LineCode = request.LineCode.Trim(),
                PlannedQty = Math.Max(0, request.PlannedQty),
                Unit = string.IsNullOrWhiteSpace(request.Unit) ? "PCS" : request.Unit.Trim(),
                PlanDate = request.PlanDate.Date,
                Status = CuttingListStatus.WAITING
            };
            _db.CuttingLists.Add(item);
            await _db.SaveChangesAsync();
            return ApiCreated(item, "Cutting list created successfully.");
        }
        catch (Exception ex)
        {
            return ApiBadRequest(ex);
        }
    }

    [HttpGet("pic-cards")]
    public async Task<IActionResult> PicCards()
    {
        return ApiOk(await _db.PicCards.AsNoTracking().OrderBy(x => x.FullName).ToListAsync());
    }

    [HttpPost("pic-cards")]
    public async Task<IActionResult> RegisterPicCard([FromBody] RegisterPicCardRequest request)
    {
        try
        {
            var cardUid = request.CardUid.Trim();
            var employeeNo = request.EmployeeNo.Trim();
            var fullName = request.FullName.Trim();

            if (string.IsNullOrWhiteSpace(cardUid))
            {
                throw new ArgumentException("Scan ID is required.");
            }

            if (string.IsNullOrWhiteSpace(employeeNo))
            {
                throw new ArgumentException("NIK is required.");
            }

            if (string.IsNullOrWhiteSpace(fullName))
            {
                throw new ArgumentException("Nama is required.");
            }

            if (await _db.PicCards.AnyAsync(x => x.CardUid == cardUid))
            {
                throw new InvalidOperationException("Scan ID is already registered.");
            }

            if (await _db.PicCards.AnyAsync(x => x.EmployeeNo == employeeNo))
            {
                throw new InvalidOperationException("NIK is already registered.");
            }

            var item = new PicCard
            {
                CardUid = cardUid,
                EmployeeNo = employeeNo,
                FullName = fullName,
                Department = "Production",
                Shift = "General",
                IsActive = true,
                CreatedAt = DateTime.Now
            };

            _db.PicCards.Add(item);
            await _db.SaveChangesAsync();
            return ApiCreated(item, "Operator card registered successfully.");
        }
        catch (Exception ex)
        {
            return ApiBadRequest(ex);
        }
    }

    [HttpPost("pic-cards/{id:int}/deactivate")]
    public async Task<IActionResult> DeactivatePicCard(int id)
    {
        try
        {
            var item = await _db.PicCards.FindAsync(id);
            if (item is null)
            {
                return ApiNotFound("Operator card was not found.");
            }

            var now = DateTime.Now;
            item.IsActive = false;

            var activeOperators = await _db.ProductionActiveOperators
                .Where(x => x.PicCardId == id && x.IsActive)
                .ToListAsync();
            foreach (var activeOperator in activeOperators)
            {
                activeOperator.IsActive = false;
                activeOperator.RemovedAt = now;
                activeOperator.UpdatedAt = now;
            }

            await _db.SaveChangesAsync();
            return ApiOk(item, "Operator card deactivated successfully.");
        }
        catch (Exception ex)
        {
            return ApiBadRequest(ex);
        }
    }

    [HttpGet("active-operators")]
    public async Task<IActionResult> ActiveOperators()
    {
        var currentShift = await GetCurrentShift();
        var activeOperators = await ActiveOperatorQuery().ToListAsync();
        return ApiOk(ToActiveOperatorSummary(activeOperators, currentShift));
    }

    [HttpPost("active-operators/scan")]
    public async Task<IActionResult> ScanActiveOperator([FromBody] ScanPicRequest request)
    {
        try
        {
            var cardUid = request.CardUid.Trim();
            if (string.IsNullOrWhiteSpace(cardUid))
            {
                throw new ArgumentException("Operator card UID is required.");
            }

            var pic = await _db.PicCards.FirstOrDefaultAsync(x => x.CardUid == cardUid && x.IsActive);
            if (pic is null)
            {
                return ApiNotFound("PIC is not registered or the card is inactive.");
            }

            var now = DateTime.Now;
            var currentShift = await GetCurrentShift(now);
            var activeOperator = await _db.ProductionActiveOperators
                .FirstOrDefaultAsync(x => x.PicCardId == pic.Id && x.IsActive);

            if (activeOperator is null)
            {
                activeOperator = new ProductionActiveOperator
                {
                    PicCardId = pic.Id,
                    ShiftMasterId = currentShift?.Id,
                    IsActive = true,
                    ScannedAt = now,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                _db.ProductionActiveOperators.Add(activeOperator);
            }
            else
            {
                activeOperator.ShiftMasterId = currentShift?.Id;
                activeOperator.ScannedAt = now;
                activeOperator.UpdatedAt = now;
                activeOperator.RemovedAt = null;
            }

            pic.LastScannedAt = now;
            await _db.SaveChangesAsync();

            var activeOperators = await ActiveOperatorQuery().ToListAsync();
            return ApiOk(ToActiveOperatorSummary(activeOperators, currentShift), "Operator is active.");
        }
        catch (Exception ex)
        {
            return ApiBadRequest(ex);
        }
    }

    [HttpPost("active-operators/{id:long}/remove")]
    public async Task<IActionResult> RemoveActiveOperator(long id)
    {
        try
        {
            var activeOperator = await _db.ProductionActiveOperators
                .FirstOrDefaultAsync(x => x.Id == id && x.IsActive);
            if (activeOperator is null)
            {
                return ApiNotFound("Active operator was not found.");
            }

            activeOperator.IsActive = false;
            activeOperator.RemovedAt = DateTime.Now;
            activeOperator.UpdatedAt = DateTime.Now;
            await _db.SaveChangesAsync();

            var currentShift = await GetCurrentShift();
            var activeOperators = await ActiveOperatorQuery().ToListAsync();
            return ApiOk(ToActiveOperatorSummary(activeOperators, currentShift), "Operator removed from active list.");
        }
        catch (Exception ex)
        {
            return ApiBadRequest(ex);
        }
    }

    [HttpPost("active-operators/remove-all")]
    public async Task<IActionResult> RemoveAllActiveOperators()
    {
        try
        {
            var now = DateTime.Now;
            var activeOperators = await _db.ProductionActiveOperators
                .Where(x => x.IsActive)
                .ToListAsync();

            foreach (var activeOperator in activeOperators)
            {
                activeOperator.IsActive = false;
                activeOperator.RemovedAt = now;
                activeOperator.UpdatedAt = now;
            }

            await _db.SaveChangesAsync();
            var currentShift = await GetCurrentShift(now);
            return ApiOk(ToActiveOperatorSummary([], currentShift), "All active operators removed.");
        }
        catch (Exception ex)
        {
            return ApiBadRequest(ex);
        }
    }

    [HttpGet("shift-masters")]
    public async Task<IActionResult> ShiftMasters()
    {
        var sortIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["SHIFT_1"] = 1,
            ["SHIFT_2"] = 2,
            ["SHIFT_3"] = 3,
            ["LONG_SHIFT_1"] = 4,
            ["LONG_SHIFT_2"] = 5
        };
        var shifts = await _db.ShiftMasters.AsNoTracking()
            .Where(x => x.IsActive)
            .ToListAsync();

        return ApiOk(shifts
            .OrderBy(x => sortIndex.GetValueOrDefault(x.ShiftCode, int.MaxValue))
            .ThenBy(x => x.ShiftName)
            .ToList());
    }

    [HttpPut("shift-masters/{id:int}")]
    public async Task<IActionResult> UpdateShiftMaster(int id, [FromBody] UpdateShiftMasterRequest request)
    {
        try
        {
            var shift = await _db.ShiftMasters.FindAsync(id);
            if (shift is null)
            {
                return ApiNotFound("Shift master was not found.");
            }

            if (string.IsNullOrWhiteSpace(request.ShiftName))
            {
                throw new ArgumentException("Shift name is required.");
            }

            shift.ShiftName = request.ShiftName.Trim();
            shift.ShiftType = NormalizeText(request.ShiftType);
            shift.StartSchedule = ParseSchedule(request.StartSchedule);
            shift.FinishSchedule = ParseSchedule(request.FinishSchedule);
            shift.IsActive = request.IsActive;

            await _db.SaveChangesAsync();
            return ApiOk(shift, "Shift master updated successfully.");
        }
        catch (Exception ex)
        {
            return ApiBadRequest(ex);
        }
    }

    [HttpGet("area-master")]
    public async Task<IActionResult> AreaMaster([FromQuery] int page = 1, [FromQuery] int pageSize = 100, [FromQuery] bool? isActive = null)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.AreaMasters.AsNoTracking().AsQueryable();
        if (isActive.HasValue)
        {
            query = query.Where(x => x.IsActive == isActive.Value);
        }

        var totalData = await query.CountAsync();
        var totalPage = totalData == 0 ? 0 : (int)Math.Ceiling(totalData / (double)pageSize);
        var items = await query
            .OrderBy(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new
        {
            success = true,
            statusCode = StatusCodes.Status200OK,
            message = "Get area master success.",
            data = items,
            pagination = new
            {
                currentPage = page,
                pageSize,
                totalData,
                totalPage,
                hasPreviousPage = page > 1,
                hasNextPage = page < totalPage
            }
        });
    }

    [HttpPost("area-master")]
    public async Task<IActionResult> CreateAreaMaster([FromBody] SaveAreaMasterRequest request)
    {
        try
        {
            var areaCode = request.AreaCode.Trim().ToUpperInvariant();
            var areaName = request.AreaName.Trim();

            if (string.IsNullOrWhiteSpace(areaCode))
            {
                throw new ArgumentException("Area code is required.");
            }

            if (string.IsNullOrWhiteSpace(areaName))
            {
                throw new ArgumentException("Area name is required.");
            }

            if (await _db.AreaMasters.AnyAsync(x => x.AreaCode == areaCode))
            {
                throw new InvalidOperationException("Area code is already used.");
            }

            if (await _db.AreaMasters.AnyAsync(x => x.AreaName == areaName))
            {
                throw new InvalidOperationException("Area name is already used.");
            }

            var now = DateTime.Now;
            var area = new AreaMaster
            {
                AreaCode = areaCode,
                AreaName = areaName,
                Description = NormalizeText(request.Description),
                IsActive = request.IsActive,
                CreatedAt = now,
                UpdatedAt = now
            };

            _db.AreaMasters.Add(area);
            await _db.SaveChangesAsync();
            return ApiCreated(area, "Area master created successfully.");
        }
        catch (Exception ex)
        {
            return ApiBadRequest(ex);
        }
    }

    [HttpPut("area-master/{id:int}")]
    public async Task<IActionResult> UpdateAreaMaster(int id, [FromBody] SaveAreaMasterRequest request)
    {
        try
        {
            var area = await _db.AreaMasters.FindAsync(id);
            if (area is null)
            {
                return ApiNotFound("Area master was not found.");
            }

            var areaCode = request.AreaCode.Trim().ToUpperInvariant();
            var areaName = request.AreaName.Trim();

            if (string.IsNullOrWhiteSpace(areaCode))
            {
                throw new ArgumentException("Area code is required.");
            }

            if (string.IsNullOrWhiteSpace(areaName))
            {
                throw new ArgumentException("Area name is required.");
            }

            if (await _db.AreaMasters.AnyAsync(x => x.Id != id && x.AreaCode == areaCode))
            {
                throw new InvalidOperationException("Area code is already used.");
            }

            if (await _db.AreaMasters.AnyAsync(x => x.Id != id && x.AreaName == areaName))
            {
                throw new InvalidOperationException("Area name is already used.");
            }

            area.AreaCode = areaCode;
            area.AreaName = areaName;
            area.Description = NormalizeText(request.Description);
            area.IsActive = request.IsActive;
            area.UpdatedAt = DateTime.Now;

            await _db.SaveChangesAsync();
            return ApiOk(area, "Area master updated successfully.");
        }
        catch (Exception ex)
        {
            return ApiBadRequest(ex);
        }
    }

    [HttpGet("activity-logs")]
    public async Task<IActionResult> ActivityLogs([FromQuery] int? workOrderId)
    {
        var query = _db.ProductionActivityLogs.AsNoTracking()
            .Include(x => x.ProductionWorkOrder)
            .Include(x => x.PicCard)
            .AsQueryable();
        if (workOrderId.HasValue)
        {
            query = query.Where(x => x.ProductionWorkOrderId == workOrderId.Value);
        }

        var logs = await query.OrderByDescending(x => x.CreatedAt).Take(250)
            .Select(x => new
            {
                id = x.Id,
                production_work_order_id = x.ProductionWorkOrderId,
                order_number = x.ProductionWorkOrder != null ? x.ProductionWorkOrder.OrderNumber : null,
                pic_name = x.PicCard != null ? x.PicCard.FullName : null,
                activity_type = x.ActivityType,
                remarks = x.Remarks,
                created_at = x.CreatedAt
            })
            .ToListAsync();
        return ApiOk(logs);
    }

    private IQueryable<ProductionWorkOrder> WorkOrderQuery() =>
        _db.ProductionWorkOrders.AsNoTracking()
            .Include(x => x.CuttingList)
            .Include(x => x.PicCard)
            .Include(x => x.ShiftMaster)
            .Include(x => x.AreaMaster)
            .Include(x => x.Operators)
                .ThenInclude(x => x.PicCard)
            .Include(x => x.Operators)
                .ThenInclude(x => x.ShiftMaster);

    private IQueryable<ProductionWorkOrder> WorkOrderWithSnapshotsQuery() =>
        WorkOrderQuery()
            .Include(x => x.OperatorSnapshots)
                .ThenInclude(x => x.PicCard)
            .Include(x => x.OperatorSnapshots)
                .ThenInclude(x => x.ShiftMaster);

    private Task<ProductionWorkOrder?> FindWorkOrder(int id) =>
        _db.ProductionWorkOrders
            .Include(x => x.CuttingList)
            .Include(x => x.PicCard)
            .Include(x => x.ShiftMaster)
            .Include(x => x.AreaMaster)
            .Include(x => x.Operators)
                .ThenInclude(x => x.PicCard)
            .Include(x => x.Operators)
                .ThenInclude(x => x.ShiftMaster)
            .FirstOrDefaultAsync(x => x.Id == id);

    private IQueryable<ProductionActiveOperator> ActiveOperatorQuery() =>
        _db.ProductionActiveOperators.AsNoTracking()
            .Include(x => x.PicCard)
            .Include(x => x.ShiftMaster)
            .Where(x => x.IsActive)
            .OrderBy(x => x.ScannedAt);

    private async Task ReloadWorkOrder(ProductionWorkOrder order)
    {
        await _db.Entry(order).Reference(x => x.CuttingList).LoadAsync();
        await _db.Entry(order).Reference(x => x.PicCard).LoadAsync();
        await _db.Entry(order).Reference(x => x.ShiftMaster).LoadAsync();
        await _db.Entry(order).Reference(x => x.AreaMaster).LoadAsync();
        _db.Entry(order).Collection(x => x.Operators).IsLoaded = false;
        await _db.Entry(order).Collection(x => x.Operators).LoadAsync();
        foreach (var workOrderOperator in order.Operators)
        {
            await _db.Entry(workOrderOperator).Reference(x => x.PicCard).LoadAsync();
            await _db.Entry(workOrderOperator).Reference(x => x.ShiftMaster).LoadAsync();
        }
    }

    private void AddLog(int workOrderId, int? picCardId, ProductionActivityType activityType, string? remarks)
    {
        _db.ProductionActivityLogs.Add(new ProductionActivityLog
        {
            ProductionWorkOrderId = workOrderId,
            PicCardId = picCardId,
            ActivityType = activityType,
            Remarks = remarks,
            CreatedAt = DateTime.Now
        });
    }

    private async Task<List<ProductionDailyShiftOutput>> GetDailyShiftOutputs(DateTime selectedDate)
    {
        var workDates = GetPreviousWorkDates(selectedDate, 5);
        var startDate = workDates.First();
        var endDate = workDates.Last().AddDays(1);
        var completedStatusId = ProductionStatusMaster.ToId(ProductionWorkOrderStatus.FINISH);
        var completedOrders = await _db.ProductionWorkOrders.AsNoTracking()
            .Include(x => x.ShiftMaster)
            .Where(x => x.StatusMasterId == completedStatusId &&
                x.CompletedAt.HasValue &&
                x.CompletedAt.Value >= startDate &&
                x.CompletedAt.Value < endDate)
            .ToListAsync();

        return workDates.Select(workDate =>
        {
            var orders = completedOrders
                .Where(x => x.CompletedAt!.Value.Date == workDate)
                .ToList();

            return new ProductionDailyShiftOutput
            {
                Date = workDate,
                DateLabel = workDate.ToString("dd MMM"),
                Shift1Count = orders.Count(x => IsShift(x.ShiftMaster, "SHIFT_1")),
                Shift2Count = orders.Count(x => IsShift(x.ShiftMaster, "SHIFT_2")),
                Shift3Count = orders.Count(x => IsShift(x.ShiftMaster, "SHIFT_3"))
            };
        }).ToList();
    }

    private static List<DateTime> GetPreviousWorkDates(DateTime selectedDate, int totalDays)
    {
        var dates = new List<DateTime>();
        var currentDate = selectedDate.Date;
        while (dates.Count < totalDays)
        {
            if (currentDate.DayOfWeek != DayOfWeek.Saturday && currentDate.DayOfWeek != DayOfWeek.Sunday)
            {
                dates.Add(currentDate);
            }

            currentDate = currentDate.AddDays(-1);
        }

        dates.Reverse();
        return dates;
    }

    private static bool IsShift(ShiftMaster? shift, string shiftCode)
    {
        var shiftText = $"{shift?.ShiftCode} {shift?.ShiftName}".ToUpperInvariant();
        return shiftCode switch
        {
            "SHIFT_1" => shiftText.Contains("SHIFT_1") || shiftText.Contains("SHIFT 1"),
            "SHIFT_2" => shiftText.Contains("SHIFT_2") || shiftText.Contains("SHIFT 2"),
            "SHIFT_3" => shiftText.Contains("SHIFT_3") || shiftText.Contains("SHIFT 3"),
            _ => false
        };
    }

    private async Task SaveFinishOperatorSnapshots(ProductionWorkOrder order, DateTime completedAt)
    {
        var activeOperators = await ActiveOperatorQuery().ToListAsync();
        var snapshots = activeOperators.Count > 0
            ? CreateSnapshotsFromActiveOperators(order.Id, ProductionOperatorSnapshotType.FINISH, activeOperators, order.ShiftMaster, completedAt)
            : CreateSnapshotsFromWorkOrderOperators(order, ProductionOperatorSnapshotType.FINISH, completedAt);

        await ReplaceOperatorSnapshots(order.Id, ProductionOperatorSnapshotType.FINISH, snapshots);
    }

    private async Task ReplaceOperatorSnapshots(
        int workOrderId,
        ProductionOperatorSnapshotType snapshotType,
        IEnumerable<ProductionWorkOrderOperatorSnapshot> snapshots)
    {
        await ClearOperatorSnapshots(workOrderId, snapshotType);
        _db.ProductionWorkOrderOperatorSnapshots.AddRange(snapshots
            .GroupBy(x => x.PicCardId)
            .Select(x => x.OrderBy(item => item.ScannedAt).First()));
    }

    private async Task ClearOperatorSnapshots(int workOrderId, ProductionOperatorSnapshotType snapshotType)
    {
        var existingSnapshots = await _db.ProductionWorkOrderOperatorSnapshots
            .Where(x => x.ProductionWorkOrderId == workOrderId && x.SnapshotType == snapshotType)
            .ToListAsync();
        _db.ProductionWorkOrderOperatorSnapshots.RemoveRange(existingSnapshots);
    }

    private static List<ProductionWorkOrderOperatorSnapshot> CreateSnapshotsFromActiveOperators(
        int workOrderId,
        ProductionOperatorSnapshotType snapshotType,
        IEnumerable<ProductionActiveOperator> activeOperators,
        ShiftMaster? currentShift,
        DateTime snapshotAt)
    {
        var now = DateTime.Now;
        return activeOperators
            .OrderBy(x => x.ScannedAt)
            .Select(x => new ProductionWorkOrderOperatorSnapshot
            {
                ProductionWorkOrderId = workOrderId,
                PicCardId = x.PicCardId,
                ProductionActiveOperatorId = x.Id,
                ShiftMasterId = x.ShiftMasterId ?? currentShift?.Id,
                SnapshotType = snapshotType,
                ScannedAt = x.ScannedAt,
                SnapshotAt = snapshotAt,
                CreatedAt = now
            })
            .ToList();
    }

    private static List<ProductionWorkOrderOperatorSnapshot> CreateSnapshotsFromWorkOrderOperators(
        ProductionWorkOrder order,
        ProductionOperatorSnapshotType snapshotType,
        DateTime snapshotAt)
    {
        var now = DateTime.Now;
        return order.Operators
            .Where(x => x.IsActive && x.PicCard is not null)
            .OrderBy(x => x.ScannedAt)
            .Select(x => new ProductionWorkOrderOperatorSnapshot
            {
                ProductionWorkOrderId = order.Id,
                PicCardId = x.PicCardId,
                ProductionActiveOperatorId = x.ProductionActiveOperatorId,
                ShiftMasterId = x.ShiftMasterId ?? order.ShiftMasterId,
                SnapshotType = snapshotType,
                ScannedAt = x.ScannedAt,
                SnapshotAt = snapshotAt,
                CreatedAt = now
            })
            .ToList();
    }

    private static string? NormalizeText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static TimeSpan? ParseSchedule(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (TimeSpan.TryParse(value.Trim(), out var schedule))
        {
            return schedule;
        }

        throw new ArgumentException("Schedule must use HH:mm format.");
    }

    private async Task<ShiftMaster?> GetCurrentShift(DateTime? currentDateTime = null)
    {
        var now = currentDateTime ?? DateTime.Now;
        var currentTime = now.TimeOfDay;
        var shifts = await _db.ShiftMasters.AsNoTracking()
            .Where(x => x.IsActive && x.StartSchedule.HasValue && x.FinishSchedule.HasValue)
            .ToListAsync();

        return shifts
            .OrderBy(GetShiftSortIndex)
            .ThenBy(x => x.StartSchedule)
            .FirstOrDefault(x => IsTimeInShift(currentTime, x.StartSchedule!.Value, x.FinishSchedule!.Value));
    }

    private static int GetShiftSortIndex(ShiftMaster shift) => shift.ShiftCode.ToUpperInvariant() switch
    {
        "SHIFT_1" => 1,
        "SHIFT_2" => 2,
        "SHIFT_3" => 3,
        "LONG_SHIFT_1" => 4,
        "LONG_SHIFT_2" => 5,
        _ => int.MaxValue
    };

    private static bool IsTimeInShift(TimeSpan currentTime, TimeSpan start, TimeSpan finish)
    {
        if (start == finish)
        {
            return true;
        }

        return start < finish
            ? currentTime >= start && currentTime < finish
            : currentTime >= start || currentTime < finish;
    }

    private void SyncActiveOperatorsToWorkOrder(
        ProductionWorkOrder order,
        IEnumerable<ProductionActiveOperator> activeOperators,
        ShiftMaster? currentShift)
    {
        var now = DateTime.Now;
        var orderedOperators = activeOperators.OrderBy(x => x.ScannedAt).ToList();
        foreach (var activeOperator in orderedOperators)
        {
            var existingOperator = order.Operators.FirstOrDefault(x => x.IsActive && x.PicCardId == activeOperator.PicCardId);
            if (existingOperator is not null)
            {
                existingOperator.ProductionActiveOperatorId ??= activeOperator.Id;
                existingOperator.ShiftMasterId ??= currentShift?.Id;
                continue;
            }

            var workOrderOperator = new ProductionWorkOrderOperator
            {
                ProductionWorkOrderId = order.Id,
                PicCardId = activeOperator.PicCardId,
                ProductionActiveOperatorId = activeOperator.Id,
                ShiftMasterId = currentShift?.Id,
                IsActive = true,
                ScannedAt = now,
                PicCard = activeOperator.PicCard
            };
            order.Operators.Add(workOrderOperator);
            _db.ProductionWorkOrderOperators.Add(workOrderOperator);
        }

        order.PicCardId = orderedOperators.FirstOrDefault()?.PicCardId ?? order.PicCardId;
    }

    private static ActiveOperatorSummaryResponse ToActiveOperatorSummary(
        List<ProductionActiveOperator> activeOperators,
        ShiftMaster? currentShift)
    {
        return new ActiveOperatorSummaryResponse
        {
            CurrentShift = currentShift,
            HasShiftChanged = currentShift is not null &&
                activeOperators.Any(x => x.ShiftMasterId.HasValue && x.ShiftMasterId.Value != currentShift.Id),
            Operators = activeOperators.Select(ToActiveOperatorResponse).ToList()
        };
    }

    private static ProductionActiveOperatorResponse ToActiveOperatorResponse(ProductionActiveOperator activeOperator)
    {
        var pic = activeOperator.PicCard;
        return new ProductionActiveOperatorResponse
        {
            Id = activeOperator.Id,
            PicCardId = activeOperator.PicCardId,
            CardUid = pic?.CardUid ?? string.Empty,
            EmployeeNo = pic?.EmployeeNo ?? string.Empty,
            FullName = pic?.FullName ?? string.Empty,
            Department = pic?.Department ?? string.Empty,
            OperatorShift = pic?.Shift ?? string.Empty,
            ShiftMasterId = activeOperator.ShiftMasterId,
            ShiftCode = activeOperator.ShiftMaster?.ShiftCode,
            ShiftName = activeOperator.ShiftMaster?.ShiftName,
            ShiftType = activeOperator.ShiftMaster?.ShiftType,
            ScannedAt = activeOperator.ScannedAt
        };
    }

    private static CuttingListResponse ToCuttingListResponse(CuttingList item, ProductionWorkOrder? order)
    {
        var activeOperators = order?.Operators
            .Where(x => x.IsActive && x.PicCard is not null)
            .OrderBy(x => x.ScannedAt)
            .ToList() ?? [];
        List<ProductionOperatorResponse> startOperators = order is null
            ? []
            : SnapshotOperatorResponses(order, ProductionOperatorSnapshotType.START, order.StartedAt);
        List<ProductionOperatorResponse> finishOperators = order is null
            ? []
            : SnapshotOperatorResponses(order, ProductionOperatorSnapshotType.FINISH, order.CompletedAt);

        return new CuttingListResponse
        {
            Id = item.Id,
            CuttingListNo = item.CuttingListNo,
            ProductCode = item.ProductCode,
            ProductName = item.ProductName,
            LineCode = item.LineCode,
            PlannedQty = item.PlannedQty,
            Unit = item.Unit,
            PlanDate = item.PlanDate,
            Status = item.Status,
            CreatedAt = item.CreatedAt,
            OrderNumber = order?.OrderNumber,
            StartedAt = order?.StartedAt,
            CompletedAt = order?.CompletedAt,
            Operators = order is null ? [] : ToOperatorResponses(order, activeOperators),
            StartOperators = startOperators,
            FinishOperators = finishOperators
        };
    }

    private static List<ProductionOperatorResponse> SnapshotOperatorResponses(
        ProductionWorkOrder order,
        ProductionOperatorSnapshotType snapshotType,
        DateTime? fallbackTimestamp)
    {
        var snapshots = order.OperatorSnapshots
            .Where(x => x.SnapshotType == snapshotType && x.PicCard is not null)
            .OrderBy(x => x.ScannedAt)
            .ToList();

        if (snapshots.Count > 0)
        {
            return ToOperatorResponses(order, snapshots);
        }

        return fallbackTimestamp.HasValue
            ? ToOperatorResponses(order, OperatorsAt(order, fallbackTimestamp.Value))
            : [];
    }

    private static List<ProductionWorkOrderOperator> OperatorsAt(ProductionWorkOrder order, DateTime timestamp)
    {
        var operators = order.Operators
            .Where(x => x.PicCard is not null &&
                x.ScannedAt <= timestamp.AddSeconds(1) &&
                (!x.RemovedAt.HasValue || x.RemovedAt.Value > timestamp))
            .OrderBy(x => x.ScannedAt)
            .ToList();

        return operators.Count > 0
            ? operators
            : order.Operators.Where(x => x.IsActive && x.PicCard is not null).OrderBy(x => x.ScannedAt).ToList();
    }

    private static List<ProductionOperatorResponse> ToOperatorResponses(
        ProductionWorkOrder order,
        IEnumerable<ProductionWorkOrderOperator> operators)
    {
        return operators
            .Select(x => new ProductionOperatorResponse
            {
                Id = x.Id,
                PicCardId = x.PicCardId,
                CardUid = x.PicCard!.CardUid,
                EmployeeNo = x.PicCard.EmployeeNo,
                FullName = x.PicCard.FullName,
                Department = x.PicCard.Department,
                Shift = x.PicCard.Shift,
                WorkShiftCode = x.ShiftMaster?.ShiftCode ?? order.ShiftMaster?.ShiftCode,
                WorkShiftName = x.ShiftMaster?.ShiftName ?? order.ShiftMaster?.ShiftName,
                WorkShiftType = x.ShiftMaster?.ShiftType ?? order.ShiftMaster?.ShiftType,
                ScannedAt = x.ScannedAt
            })
            .ToList();
    }

    private static List<ProductionOperatorResponse> ToOperatorResponses(
        ProductionWorkOrder order,
        IEnumerable<ProductionWorkOrderOperatorSnapshot> snapshots)
    {
        return snapshots
            .Select(x => new ProductionOperatorResponse
            {
                Id = x.Id,
                PicCardId = x.PicCardId,
                CardUid = x.PicCard!.CardUid,
                EmployeeNo = x.PicCard.EmployeeNo,
                FullName = x.PicCard.FullName,
                Department = x.PicCard.Department,
                Shift = x.PicCard.Shift,
                WorkShiftCode = x.ShiftMaster?.ShiftCode ?? order.ShiftMaster?.ShiftCode,
                WorkShiftName = x.ShiftMaster?.ShiftName ?? order.ShiftMaster?.ShiftName,
                WorkShiftType = x.ShiftMaster?.ShiftType ?? order.ShiftMaster?.ShiftType,
                ScannedAt = x.ScannedAt
            })
            .ToList();
    }

    private static ProductionWorkOrderResponse ToResponse(ProductionWorkOrder order)
    {
        var activeOperators = order.Operators
            .Where(x => x.IsActive)
            .OrderBy(x => x.ScannedAt)
            .ToList();
        var primaryOperator = activeOperators.FirstOrDefault()?.PicCard ?? order.PicCard;

        return new ProductionWorkOrderResponse
        {
            Id = order.Id,
            OrderNumber = order.OrderNumber,
            CuttingListId = order.CuttingListId,
            CuttingListNo = order.CuttingList?.CuttingListNo ?? string.Empty,
            ProductCode = order.CuttingList?.ProductCode ?? string.Empty,
            ProductName = order.CuttingList?.ProductName ?? string.Empty,
            PicCardId = primaryOperator?.Id,
            PicName = primaryOperator?.FullName,
            EmployeeNo = primaryOperator?.EmployeeNo,
            OperatorShift = primaryOperator?.Shift,
            OperatorDepartment = primaryOperator?.Department,
            WorkShiftCode = order.ShiftMaster?.ShiftCode,
            WorkShiftName = order.ShiftMaster?.ShiftName,
            WorkShiftType = order.ShiftMaster?.ShiftType,
            AreaMasterId = order.AreaMasterId,
            AreaCode = order.AreaMaster?.AreaCode,
            AreaName = order.AreaMaster?.AreaName,
            Operators = activeOperators
                .Where(x => x.PicCard is not null)
                .Select(x => new ProductionOperatorResponse
                {
                    Id = x.Id,
                    PicCardId = x.PicCardId,
                    CardUid = x.PicCard!.CardUid,
                    EmployeeNo = x.PicCard.EmployeeNo,
                    FullName = x.PicCard.FullName,
                    Department = x.PicCard.Department,
                    Shift = x.PicCard.Shift,
                    WorkShiftCode = x.ShiftMaster?.ShiftCode ?? order.ShiftMaster?.ShiftCode,
                    WorkShiftName = x.ShiftMaster?.ShiftName ?? order.ShiftMaster?.ShiftName,
                    WorkShiftType = x.ShiftMaster?.ShiftType ?? order.ShiftMaster?.ShiftType,
                    ScannedAt = x.ScannedAt
                })
                .ToList(),
            LineCode = order.LineCode,
            ActualQty = order.CompletedAt.HasValue ? order.ActualQty : 0,
            RejectQty = order.CompletedAt.HasValue ? order.RejectQty : 0,
            Status = order.Status,
            PlanDate = order.CuttingList?.PlanDate ?? order.CreatedAt.Date,
            StartedAt = order.StartedAt,
            CompletedAt = order.CompletedAt,
            UpdatedAt = order.UpdatedAt
        };
    }
}
