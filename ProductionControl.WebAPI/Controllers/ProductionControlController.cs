using ProductionControl.Domain.Production;
using ProductionControl.Persistence.Context;
using ProductionControl.WebAPI.Reports;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace ProductionControl.WebAPI.Controllers;

[ApiController]
[Route("api/production")]
public class ProductionControlController : ApiControllerBase
{
    private readonly ProductionControlDbContext _db;
    private readonly IWebHostEnvironment _environment;
    private readonly IHttpClientFactory _httpClientFactory;
    private const string ShiageLotNoSettingKey = "shiage_lot_no";
    private const string InternalSystemAuthSettingKey = "internal_system_auth";
    private const string InternalSystemRefreshSettingKey = "internal_system_refresh";
    private static readonly SemaphoreSlim ExternalTokenLock = new(1, 1);
    private static ExternalAuthToken? CachedExternalToken;

    public ProductionControlController(
        ProductionControlDbContext db,
        IWebHostEnvironment environment,
        IHttpClientFactory httpClientFactory)
    {
        _db = db;
        _environment = environment;
        _httpClientFactory = httpClientFactory;
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard([FromQuery] DateTime? date)
    {
        try
        {
            var selectedDate = (date ?? DateTime.Today).Date;
            var nextDate = selectedDate.AddDays(1);
            var ordersForDate = await WorkOrderQuery()
                .Where(x =>
                    (x.PlanDate >= selectedDate && x.PlanDate < nextDate) ||
                    (x.CreatedAt >= selectedDate && x.CreatedAt < nextDate) ||
                    (x.UpdatedAt >= selectedDate && x.UpdatedAt < nextDate) ||
                    (x.StartedAt.HasValue && x.StartedAt.Value >= selectedDate && x.StartedAt.Value < nextDate) ||
                    (x.CompletedAt.HasValue && x.CompletedAt.Value >= selectedDate && x.CompletedAt.Value < nextDate))
                .OrderBy(x => x.LineCode)
                .ThenBy(x => x.OrderNumber)
                .ToListAsync();
            var latestOrders = await WorkOrderQuery()
                .OrderByDescending(x => x.UpdatedAt)
                .ThenByDescending(x => x.Id)
                .Take(50)
                .ToListAsync();

            var completedOrders = ordersForDate
                .Where(x => x.CompletedAt.HasValue &&
                    x.CompletedAt.Value >= selectedDate &&
                    x.CompletedAt.Value < nextDate)
                .ToList();
            var actualQty = completedOrders.Sum(x => x.ActualQty);
            var dailyShiftOutputs = await GetDailyShiftOutputs(selectedDate);

            return ApiOk(new ProductionDashboardSummary
            {
                TotalWorkOrders = ordersForDate.Count,
                WaitingWorkOrders = ordersForDate.Count(x => x.Status == ProductionWorkOrderStatus.WAITING),
                RunningWorkOrders = ordersForDate.Count(x => x.Status == ProductionWorkOrderStatus.IN_PROGRESS),
                CompletedWorkOrders = completedOrders.Count,
                ActualQty = actualQty,
                RejectQty = completedOrders.Sum(x => x.RejectQty),
                WorkOrders = latestOrders.Select(ToDashboardResponse).ToList(),
                DailyShiftOutputs = dailyShiftOutputs
            });
        }
        catch (Exception ex)
        {
            return ApiBadRequest(ex);
        }
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
            query = query.Where(x => x.PlanDate >= selectedDate && x.PlanDate < nextDate);
        }

        var items = await query.OrderByDescending(x => x.UpdatedAt).ToListAsync();
        return ApiOk(items.Select(ToResponse).ToList());
    }

    [HttpPost("work-orders")]
    public async Task<IActionResult> CreateWorkOrder([FromBody] CreateProductionWorkOrderRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.OrderNumber))
            {
                throw new ArgumentException("Order number is required.");
            }

            if (string.IsNullOrWhiteSpace(request.LineCode))
            {
                throw new ArgumentException("Line is required.");
            }

            if (await _db.ProductionWorkOrders.AnyAsync(x => x.OrderNumber == request.OrderNumber.Trim()))
            {
                throw new InvalidOperationException("Order number is already used.");
            }

            var order = new ProductionWorkOrder
            {
                OrderNumber = request.OrderNumber.Trim(),
                LineCode = request.LineCode.Trim(),
                PlanDate = request.PlanDate?.Date ?? DateTime.Today,
                TargetQty = 0,
                Status = ProductionWorkOrderStatus.WAITING
            };
            _db.ProductionWorkOrders.Add(order);
            await _db.SaveChangesAsync();
            await UpsertReleaseProductionOrderDetail(order.Id, request.OrderNumber, request.LotNo, request.ProjectNo, request.Weight);
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
            var rawScanCode = NormalizeText(request.LotNo) ?? NormalizeText(request.OrderNumber);
            var code = ExtractLotNoFromScan(rawScanCode);
            if (string.IsNullOrWhiteSpace(code))
            {
                throw new ArgumentException("Lot No is required.");
            }

            var existingOrder = await WorkOrderQuery()
                .FirstOrDefaultAsync(x =>
                    x.OrderNumber == code ||
                    x.ReleaseProductionOrderDetail != null && x.ReleaseProductionOrderDetail.LotNo == code);

            if (existingOrder is not null)
            {
                AddLog(existingOrder.Id, ProductionActivityType.CUTTING_LIST_SCAN, $"Lot No scan {code}");
                await _db.SaveChangesAsync();
                return ApiOk(ToResponse(existingOrder), "Lot No found.");
            }

            var shiageResult = await FetchShiageLotNo(code);
            if (shiageResult is null)
            {
                return ApiNotFound("Lot No / order number was not found.");
            }

            var orderNumber = NormalizeText(shiageResult.OrderNo) ?? NormalizeText(shiageResult.LotNo) ?? code;
            var lotNo = NormalizeText(shiageResult.LotNo) ?? code;
            var order = await WorkOrderQuery()
                .FirstOrDefaultAsync(x =>
                    x.OrderNumber == orderNumber ||
                    x.ReleaseProductionOrderDetail != null && x.ReleaseProductionOrderDetail.LotNo == lotNo);

            if (order is null)
            {
                order = new ProductionWorkOrder
                {
                    OrderNumber = orderNumber,
                    LineCode = NormalizeLineCode(shiageResult.Line),
                    PlanDate = shiageResult.CutPlan ?? DateTime.Today,
                    TargetQty = 0,
                    Status = ProductionWorkOrderStatus.WAITING
                };
                _db.ProductionWorkOrders.Add(order);
                await _db.SaveChangesAsync();
            }

            await UpsertReleaseProductionOrderDetail(
                order.Id,
                orderNumber,
                lotNo,
                shiageResult.ProjectNo,
                shiageResult.Weight,
                shiageResult.ProjectName);
            var logCode = rawScanCode == code ? code : $"{rawScanCode} -> {code}";
            AddLog(order.Id, ProductionActivityType.CUTTING_LIST_SCAN, $"Lot No scan {logCode}");
            await _db.SaveChangesAsync();
            await ReloadWorkOrder(order);

            return ApiCreated(ToResponse(order), "Lot No found from Shiage and registered.");
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

            var operatorCode = NormalizeText(request.CardUid);
            var pic = await FindActivePicCard(operatorCode);
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

            order.UpdatedAt = DateTime.Now;
            pic.LastScannedAt = DateTime.Now;
            AddLog(order.Id, ProductionActivityType.PIC_SCAN, $"Operator {pic.EmployeeNo} - {pic.FullName}");
            await _db.SaveChangesAsync();
            await ReloadWorkOrder(order);
            return ApiOk(ToResponse(order), "Operator added successfully.");
        }
        catch (Exception ex)
        {
            return ApiBadRequest(ex);
        }
    }

    [HttpGet("settings/shiage-lot-no")]
    public async Task<IActionResult> GetShiageLotNoSetting()
    {
        var setting = await GetIntegrationSettingEntity(ShiageLotNoSettingKey);
        return ApiOk(ToSettingResponse(setting));
    }

    [HttpPut("settings/shiage-lot-no")]
    public async Task<IActionResult> SaveShiageLotNoSetting([FromBody] SaveProductionIntegrationSettingRequest request)
    {
        return await SaveIntegrationSetting(ShiageLotNoSettingKey, request, "Setting endpoint Shiage berhasil disimpan.");
    }

    [HttpGet("settings/integration/{settingKey}")]
    public async Task<IActionResult> GetIntegrationSetting(string settingKey)
    {
        try
        {
            var normalizedSettingKey = NormalizeSettingKey(settingKey);
            var setting = await GetIntegrationSettingEntity(normalizedSettingKey);
            return ApiOk(ToSettingResponse(setting));
        }
        catch (Exception ex)
        {
            return ApiBadRequest(ex);
        }
    }

    [HttpPut("settings/integration/{settingKey}")]
    public async Task<IActionResult> SaveIntegrationSetting(string settingKey, [FromBody] SaveProductionIntegrationSettingRequest request)
    {
        try
        {
            var normalizedSettingKey = NormalizeSettingKey(settingKey);
            return await SaveIntegrationSetting(normalizedSettingKey, request, "Setting endpoint berhasil disimpan.");
        }
        catch (Exception ex)
        {
            return ApiBadRequest(ex);
        }
    }

    [HttpPost("settings/integration/internal-system-auth/test-login")]
    public async Task<IActionResult> TestInternalSystemLogin([FromBody] SaveProductionIntegrationSettingRequest request)
    {
        try
        {
            var storedSetting = await GetIntegrationSettingEntity(InternalSystemAuthSettingKey);
            var baseUrl = NormalizeBaseUrl(request.BaseUrl) ?? NormalizeBaseUrl(storedSetting.BaseUrl);
            var endpointPath = NormalizeEndpointPath(
                request.EndpointPath,
                storedSetting.EndpointPath is { Length: > 0 } ? storedSetting.EndpointPath : GetDefaultEndpointPath(InternalSystemAuthSettingKey));
            var username = NormalizeText(request.Username) ?? NormalizeText(storedSetting.Username);
            var password = NormalizeText(request.Password) ?? NormalizeText(storedSetting.Password);

            if (baseUrl is null)
            {
                throw new ArgumentException("Base URL is required.");
            }

            if (username is null)
            {
                throw new ArgumentException("Username is required.");
            }

            if (password is null)
            {
                throw new ArgumentException("Password is required.");
            }

            var url = $"{baseUrl}{endpointPath}";
            using var response = await _httpClientFactory.CreateClient().PostAsJsonAsync(url, new
            {
                email = username,
                password
            });
            var content = await response.Content.ReadAsStringAsync();
            var message = GetInternalLoginMessage(content) ?? response.ReasonPhrase ?? "Login request completed.";
            var token = GetInternalLoginToken(content);
            var refreshToken = GetInternalRefreshToken(content);
            var refresh = response.IsSuccessStatusCode && refreshToken is not null
                ? await TestInternalSystemRefresh(refreshToken, baseUrl)
                : null;

            if (response.IsSuccessStatusCode && token is not null)
            {
                CachedExternalToken = new ExternalAuthToken(
                    token,
                    refreshToken,
                    ResolveTokenExpiry(token),
                    $"{baseUrl}|{username}");
            }

            return ApiOk(new InternalSystemLoginTestResponse
            {
                Url = url,
                StatusCode = (int)response.StatusCode,
                Success = response.IsSuccessStatusCode,
                Message = message,
                TokenPreview = MaskToken(token),
                RefreshTokenPreview = MaskToken(refreshToken),
                Refresh = refresh,
                ResponsePreview = content.Length > 500 ? $"{content[..500]}..." : content
            }, response.IsSuccessStatusCode ? "Login external endpoint berhasil." : "Login external endpoint gagal.");
        }
        catch (Exception ex)
        {
            return ApiBadRequest(ex);
        }
    }

    [HttpPost("settings/integration/shiage-lot-no/test")]
    public async Task<IActionResult> TestShiageLotNoEndpoint([FromBody] TestShiageEndpointRequest request)
    {
        try
        {
            var lotNo = ExtractLotNoFromScan(request.LotNo);
            var setting = new ProductionIntegrationSetting
            {
                SettingKey = ShiageLotNoSettingKey,
                BaseUrl = NormalizeBaseUrl(request.BaseUrl) ?? string.Empty,
                EndpointPath = NormalizeEndpointPath(request.EndpointPath, GetDefaultEndpointPath(ShiageLotNoSettingKey)),
                FilterFieldName = NormalizeText(request.FilterFieldName) ?? GetDefaultFilterFieldName(ShiageLotNoSettingKey),
                Top = Math.Clamp(request.Top, 1, 1000),
                Skip = Math.Max(0, request.Skip),
                IsActive = request.IsActive
            };

            if (string.IsNullOrWhiteSpace(setting.BaseUrl))
            {
                throw new ArgumentException("Base URL is required.");
            }

            var url = BuildShiageUrl(setting, lotNo);
            using var response = await SendShiageRequest(url);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(BuildShiageErrorMessage(url, response, content));
            }

            using var document = JsonDocument.Parse(content);
            var rows = ResolveShiageDataRows(document.RootElement)
                .Select(ToFabShiageProductionResultResponse)
                .Take(10)
                .ToList();

            return ApiOk(rows, $"Endpoint berhasil diakses. {rows.Count} row diterima.");
        }
        catch (Exception ex)
        {
            return ApiBadRequest(ex);
        }
    }

    private async Task<InternalSystemRefreshTestResult> TestInternalSystemRefresh(string refreshToken, string authBaseUrl)
    {
        var storedRefreshSetting = await GetIntegrationSettingEntity(InternalSystemRefreshSettingKey);
        if (!storedRefreshSetting.IsActive)
        {
            return new InternalSystemRefreshTestResult
            {
                Success = false,
                Message = "Refresh endpoint tidak aktif."
            };
        }

        var refreshBaseUrl = NormalizeBaseUrl(storedRefreshSetting.BaseUrl) ?? authBaseUrl;
        var refreshEndpointPath = NormalizeEndpointPath(
            storedRefreshSetting.EndpointPath,
            storedRefreshSetting.EndpointPath is { Length: > 0 }
                ? storedRefreshSetting.EndpointPath
                : GetDefaultEndpointPath(InternalSystemRefreshSettingKey));
        var separator = refreshEndpointPath.Contains('?') ? "&" : "?";
        var refreshUrl = $"{refreshBaseUrl}{refreshEndpointPath}{separator}refresh_token={Uri.EscapeDataString(refreshToken)}";

        using var response = await _httpClientFactory.CreateClient().PostAsync(refreshUrl, null);
        var content = await response.Content.ReadAsStringAsync();
        var message = GetInternalLoginMessage(content) ?? response.ReasonPhrase ?? "Refresh request completed.";
        var token = GetInternalLoginToken(content);

        return new InternalSystemRefreshTestResult
        {
            Url = refreshUrl,
            StatusCode = (int)response.StatusCode,
            Success = response.IsSuccessStatusCode,
            Message = message,
            TokenPreview = MaskToken(token),
            ResponsePreview = content.Length > 500 ? $"{content[..500]}..." : content
        };
    }

    private async Task<IActionResult> SaveIntegrationSetting(
        string settingKey,
        SaveProductionIntegrationSettingRequest request,
        string successMessage)
    {
        try
        {
            var baseUrl = NormalizeBaseUrl(request.BaseUrl);
            var endpointPath = NormalizeEndpointPath(request.EndpointPath, GetDefaultEndpointPath(settingKey));
            var filterFieldName = NormalizeText(request.FilterFieldName) ?? GetDefaultFilterFieldName(settingKey);

            if (baseUrl is null && settingKey != InternalSystemRefreshSettingKey)
            {
                throw new ArgumentException("Base URL is required.");
            }

            var setting = await _db.ProductionIntegrationSettings
                .FirstOrDefaultAsync(x => x.SettingKey == settingKey);
            var now = DateTime.Now;

            if (setting is null)
            {
                setting = new ProductionIntegrationSetting
                {
                    SettingKey = settingKey,
                    CreatedAt = now
                };
                _db.ProductionIntegrationSettings.Add(setting);
            }

            setting.BaseUrl = baseUrl ?? string.Empty;
            setting.EndpointPath = endpointPath;
            setting.Username = NormalizeText(request.Username);
            if (!string.IsNullOrWhiteSpace(request.Password))
            {
                setting.Password = request.Password.Trim();
            }
            setting.FilterFieldName = filterFieldName;
            setting.Top = Math.Clamp(request.Top, 1, 1000);
            setting.Skip = Math.Max(0, request.Skip);
            setting.IsActive = request.IsActive;
            setting.UpdatedAt = now;

            await _db.SaveChangesAsync();
            return ApiOk(ToSettingResponse(setting), successMessage);
        }
        catch (Exception ex)
        {
            return ApiBadRequest(ex);
        }
    }

    [HttpGet("fab-shiage-prod-res")]
    public async Task<IActionResult> FabShiageProductionResults([FromQuery] string? lotNo, [FromQuery] int top = 50, [FromQuery] int skip = 0)
    {
        top = Math.Clamp(top, 1, 1000);
        skip = Math.Max(skip, 0);

        var normalizedLotNo = NormalizeText(lotNo);
        var query = _db.ReleaseProductionOrderDetails.AsNoTracking()
            .Include(x => x.ProductionWorkOrder)
            .Include(x => x.ProjectMaster)
            .AsQueryable();

        if (normalizedLotNo is not null)
        {
            query = query.Where(x => x.LotNo == normalizedLotNo);
        }

        var rows = await query
            .OrderBy(x => x.LotNo)
            .Skip(skip)
            .Take(top)
            .Select(x => new FabShiageProductionResultResponse
            {
                ProjectNo = x.ProjectNo,
                ProjectName = x.ProjectMaster == null ? null : x.ProjectMaster.ProjectName,
                OrderNo = x.OrderNo ?? (x.ProductionWorkOrder == null ? null : x.ProductionWorkOrder.OrderNumber),
                LotNo = x.LotNo,
                Weight = x.Weight
            })
            .ToListAsync();

        return ApiOk(rows);
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

            AddLog(order.Id, ProductionActivityType.OPERATOR_REMOVE, $"Operator {activeOperator.PicCard?.EmployeeNo} - {activeOperator.PicCard?.FullName} removed");
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
            var lineMasterId = request?.AreaMasterId ?? order.AreaMasterId;
            if (!lineMasterId.HasValue)
            {
                throw new InvalidOperationException("Pilih line sebelum Start Order Number.");
            }

            var line = await _db.AreaMasters.FirstOrDefaultAsync(x => x.Id == lineMasterId.Value && x.IsActive);
            if (line is null)
            {
                throw new InvalidOperationException("Line tidak ditemukan atau sudah tidak aktif.");
            }

            var startedAt = order.StartedAt ?? DateTime.Now;
            SyncActiveOperatorsToWorkOrder(order, activeOperators, currentShift);

            order.Status = ProductionWorkOrderStatus.IN_PROGRESS;
            order.StartedAt = startedAt;
            order.ShiftMasterId = currentShift?.Id;
            order.AreaMasterId = line.Id;
            order.AreaMaster = line;
            order.LineCode = line.AreaCode;
            order.UpdatedAt = DateTime.Now;
            await ReplaceOperatorSnapshots(
                order.Id,
                ProductionOperatorSnapshotType.START,
                CreateSnapshotsFromActiveOperators(order.Id, ProductionOperatorSnapshotType.START, activeOperators, currentShift, startedAt));
            AddLog(order.Id, ProductionActivityType.WORK_START, $"Production work started at {line.AreaName}");
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
            AddLog(order.Id, ProductionActivityType.PRODUCTION_UPDATE, request.Remarks ?? $"Actual {actualQty}, reject {rejectQty}");
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
            await SaveFinishOperatorSnapshots(order, completedAt);
            AddLog(order.Id, ProductionActivityType.WORK_COMPLETE, request?.Remarks ?? "Production work completed");
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
            await SaveFinishOperatorSnapshots(order, completedAt);
            AddLog(order.Id, ProductionActivityType.WORK_COMPLETE, "Production work finished");
            await _db.SaveChangesAsync();
            return ApiOk(ToResponse(order), "Work order finished.");
        }
        catch (Exception ex)
        {
            return ApiBadRequest(ex);
        }
    }

    [HttpPost("work-orders/{id:int}/cancel-finish")]
    public async Task<IActionResult> CancelFinish(int id, [FromBody] UpdateProductionRequest? request)
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
            await ClearOperatorSnapshots(order.Id, ProductionOperatorSnapshotType.FINISH);
            AddLog(order.Id, ProductionActivityType.FINISH_CANCELLED, request?.Remarks ?? "Production finish cancelled");
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
        return ApiOk(await GetProductionHistoryRows(null, date, null, null, null));
    }

    [HttpGet("cutting-lists/export")]
    public async Task<IActionResult> ExportCuttingLists(
        [FromQuery] CuttingListStatus? status,
        [FromQuery] DateTime? date,
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate)
    {
        var rows = await GetProductionHistoryRows(status, date, startDate, endDate, null);
        var exporter = new ProductionHistoryExcelExporter();
        var fileContent = exporter.Export(rows, new ProductionHistoryExportContext(DateTime.Now, "Production History"));
        var fileName = $"Production-History-{DateTime.Now:yyyyMMdd-HHmm}.xlsx";

        return File(
            fileContent,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }

    [HttpGet("cutting-lists/{id:int}/export")]
    public async Task<IActionResult> ExportCuttingListDetail(int id)
    {
        var rows = await GetProductionHistoryRows(null, null, null, null, id);
        if (rows.Count == 0)
        {
            return ApiNotFound("Production history was not found.");
        }

        var row = rows[0];
        var exporter = new ProductionHistoryExcelExporter();
        var fileContent = exporter.ExportDetail(row, new ProductionHistoryExportContext(DateTime.Now, "Production History Detail"));
        var fileName = $"Production-History-{SanitizeFileName(row.LotNo ?? row.OrderNumber ?? row.Id.ToString())}-{DateTime.Now:yyyyMMdd-HHmm}.xlsx";

        return File(
            fileContent,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }

    private async Task<List<CuttingListResponse>> GetProductionHistoryRows(
        CuttingListStatus? status,
        DateTime? date,
        DateTime? startDate,
        DateTime? endDate,
        int? cuttingListId)
    {
        var query = WorkOrderWithSnapshotsQuery();
        if (cuttingListId.HasValue)
        {
            query = query.Where(x => x.Id == cuttingListId.Value);
        }

        if (date.HasValue)
        {
            var selectedDate = date.Value.Date;
            var nextDate = selectedDate.AddDays(1);
            query = query.Where(x => x.CreatedAt >= selectedDate && x.CreatedAt < nextDate);
        }
        else
        {
            if (startDate.HasValue)
            {
                query = query.Where(x => x.CreatedAt >= startDate.Value.Date);
            }

            if (endDate.HasValue)
            {
                query = query.Where(x => x.CreatedAt < endDate.Value.Date.AddDays(1));
            }
        }

        if (status.HasValue)
        {
            var statusMasterId = ProductionStatusMaster.ToId(status.Value);
            query = query.Where(x => x.StatusMasterId == statusMasterId);
        }

        var workOrders = await query
            .OrderByDescending(x => x.PlanDate)
            .ThenBy(x => x.LineCode)
            .ToListAsync();

        return workOrders
            .Select(ToProductionHistoryResponse)
            .ToList();
    }

    private static string SanitizeFileName(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(value.Select(ch => invalidChars.Contains(ch) ? '-' : ch).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "Detail" : sanitized;
    }

    [HttpPost("cutting-lists")]
    public async Task<IActionResult> CreateCuttingList([FromBody] CreateCuttingListRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.LineCode))
            {
                throw new ArgumentException("Line is required.");
            }

            var orderNumber = NormalizeText(request.LotNo) ?? $"WO-{DateTime.Now:yyyyMMddHHmmssfff}";
            if (await _db.ProductionWorkOrders.AnyAsync(x => x.OrderNumber == orderNumber))
            {
                throw new InvalidOperationException("Order number is already used.");
            }

            var item = new ProductionWorkOrder
            {
                OrderNumber = orderNumber,
                LineCode = request.LineCode.Trim(),
                PlanDate = request.PlanDate.Date,
                TargetQty = Math.Max(0, request.PlannedQty),
                Status = ProductionWorkOrderStatus.WAITING
            };
            _db.ProductionWorkOrders.Add(item);
            await _db.SaveChangesAsync();
            await UpsertReleaseProductionOrderDetail(item.Id, orderNumber, request.LotNo, request.ProjectNo, request.Weight);
            await _db.SaveChangesAsync();
            await ReloadWorkOrder(item);
            return ApiCreated(ToProductionHistoryResponse(item), "Production work order created successfully.");
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
            var operatorCode = NormalizeText(request.CardUid);
            if (operatorCode is null)
            {
                throw new ArgumentException("Operator card UID is required.");
            }

            var pic = await FindActivePicCard(operatorCode);
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

    [HttpGet("line-master")]
    public async Task<IActionResult> LineMaster([FromQuery] int page = 1, [FromQuery] int pageSize = 100, [FromQuery] bool? isActive = null)
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
            message = "Get line master success.",
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

    [HttpPost("line-master")]
    public async Task<IActionResult> CreateLineMaster([FromBody] SaveAreaMasterRequest request)
    {
        try
        {
            var lineNo = request.RawLineNo.Trim().ToUpperInvariant();
            var lineName = request.RawLineName.Trim();

            if (string.IsNullOrWhiteSpace(lineNo))
            {
                throw new ArgumentException("Line No is required.");
            }

            if (string.IsNullOrWhiteSpace(lineName))
            {
                throw new ArgumentException("Line Name is required.");
            }

            if (await _db.AreaMasters.AnyAsync(x => x.AreaCode == lineNo))
            {
                throw new InvalidOperationException("Line No is already used.");
            }

            if (await _db.AreaMasters.AnyAsync(x => x.AreaName == lineName))
            {
                throw new InvalidOperationException("Line Name is already used.");
            }

            var now = DateTime.Now;
            var line = new AreaMaster
            {
                AreaCode = lineNo,
                AreaName = lineName,
                Description = NormalizeText(request.Description),
                IsActive = request.IsActive,
                CreatedAt = now,
                UpdatedAt = now
            };

            _db.AreaMasters.Add(line);
            await _db.SaveChangesAsync();
            return ApiCreated(line, "Line master created successfully.");
        }
        catch (Exception ex)
        {
            return ApiBadRequest(ex);
        }
    }

    [HttpPut("line-master/{id:int}")]
    public async Task<IActionResult> UpdateLineMaster(int id, [FromBody] SaveAreaMasterRequest request)
    {
        try
        {
            var area = await _db.AreaMasters.FindAsync(id);
            if (area is null)
            {
                return ApiNotFound("Line master was not found.");
            }

            var lineNo = request.RawLineNo.Trim().ToUpperInvariant();
            var lineName = request.RawLineName.Trim();

            if (string.IsNullOrWhiteSpace(lineNo))
            {
                throw new ArgumentException("Line No is required.");
            }

            if (string.IsNullOrWhiteSpace(lineName))
            {
                throw new ArgumentException("Line Name is required.");
            }

            if (await _db.AreaMasters.AnyAsync(x => x.Id != id && x.AreaCode == lineNo))
            {
                throw new InvalidOperationException("Line No is already used.");
            }

            if (await _db.AreaMasters.AnyAsync(x => x.Id != id && x.AreaName == lineName))
            {
                throw new InvalidOperationException("Line Name is already used.");
            }

            area.AreaCode = lineNo;
            area.AreaName = lineName;
            area.Description = NormalizeText(request.Description);
            area.IsActive = request.IsActive;
            area.UpdatedAt = DateTime.Now;

            await _db.SaveChangesAsync();
            return ApiOk(area, "Line master updated successfully.");
        }
        catch (Exception ex)
        {
            return ApiBadRequest(ex);
        }
    }

    [HttpGet("unit-master")]
    public async Task<IActionResult> UnitMaster([FromQuery] int page = 1, [FromQuery] int pageSize = 100, [FromQuery] bool? isActive = null)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.UnitMasters.AsNoTracking().AsQueryable();
        if (isActive.HasValue)
        {
            query = query.Where(x => x.IsActive == isActive.Value);
        }

        var totalData = await query.CountAsync();
        var totalPage = totalData == 0 ? 0 : (int)Math.Ceiling(totalData / (double)pageSize);
        var items = await query
            .OrderBy(x => x.UnitName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new
        {
            success = true,
            statusCode = StatusCodes.Status200OK,
            message = "Get unit master success.",
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

    [HttpPost("unit-master")]
    public async Task<IActionResult> CreateUnitMaster([FromBody] SaveUnitMasterRequest request)
    {
        try
        {
            var unitCode = request.UnitCode.Trim().ToUpperInvariant();
            var unitName = request.UnitName.Trim();

            if (string.IsNullOrWhiteSpace(unitCode))
            {
                throw new ArgumentException("Unit Code is required.");
            }

            if (string.IsNullOrWhiteSpace(unitName))
            {
                throw new ArgumentException("Unit Name is required.");
            }

            if (await _db.UnitMasters.AnyAsync(x => x.UnitCode == unitCode))
            {
                throw new InvalidOperationException("Unit Code is already used.");
            }

            if (await _db.UnitMasters.AnyAsync(x => x.UnitName == unitName))
            {
                throw new InvalidOperationException("Unit Name is already used.");
            }

            var now = DateTime.Now;
            var unit = new UnitMaster
            {
                UnitCode = unitCode,
                UnitName = unitName,
                Description = NormalizeText(request.Description),
                IsActive = request.IsActive,
                CreatedAt = now,
                UpdatedAt = now
            };

            _db.UnitMasters.Add(unit);
            await _db.SaveChangesAsync();
            return ApiCreated(unit, "Unit master created successfully.");
        }
        catch (Exception ex)
        {
            return ApiBadRequest(ex);
        }
    }

    [HttpPut("unit-master/{id:int}")]
    public async Task<IActionResult> UpdateUnitMaster(int id, [FromBody] SaveUnitMasterRequest request)
    {
        try
        {
            var unit = await _db.UnitMasters.FindAsync(id);
            if (unit is null)
            {
                return ApiNotFound("Unit master was not found.");
            }

            var unitCode = request.UnitCode.Trim().ToUpperInvariant();
            var unitName = request.UnitName.Trim();

            if (string.IsNullOrWhiteSpace(unitCode))
            {
                throw new ArgumentException("Unit Code is required.");
            }

            if (string.IsNullOrWhiteSpace(unitName))
            {
                throw new ArgumentException("Unit Name is required.");
            }

            if (await _db.UnitMasters.AnyAsync(x => x.Id != id && x.UnitCode == unitCode))
            {
                throw new InvalidOperationException("Unit Code is already used.");
            }

            if (await _db.UnitMasters.AnyAsync(x => x.Id != id && x.UnitName == unitName))
            {
                throw new InvalidOperationException("Unit Name is already used.");
            }

            unit.UnitCode = unitCode;
            unit.UnitName = unitName;
            unit.Description = NormalizeText(request.Description);
            unit.IsActive = request.IsActive;
            unit.UpdatedAt = DateTime.Now;

            await _db.SaveChangesAsync();
            return ApiOk(unit, "Unit master updated successfully.");
        }
        catch (Exception ex)
        {
            return ApiBadRequest(ex);
        }
    }

    [HttpGet("activity-logs")]
    public async Task<IActionResult> ActivityLogs(
        [FromQuery] int? workOrderId,
        [FromQuery] DateTime? date,
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate)
    {
        var range = ResolveActivityLogDateRange(date, startDate, endDate, defaultToLastSevenDays: false);
        var query = ApplyActivityLogFilters(ActivityLogReportQuery(), workOrderId, range.Start, range.EndExclusive);

        var logs = await query.OrderByDescending(x => x.CreatedAt).Take(250)
            .Select(x => new
            {
                id = x.Id,
                production_work_order_id = x.ProductionWorkOrderId,
                order_number = x.ProductionWorkOrder != null ? x.ProductionWorkOrder.OrderNumber : null,
                user_id = x.UserId,
                username = x.User != null ? x.User.Username : null,
                pic_name = x.User != null ? x.User.FullName : null,
                employee_no = x.User != null ? x.User.Username : null,
                activity_type = x.ActivityType,
                remarks = x.Remarks,
                lot_no = x.ProductionWorkOrder != null && x.ProductionWorkOrder.ReleaseProductionOrderDetail != null
                    ? x.ProductionWorkOrder.ReleaseProductionOrderDetail.LotNo
                    : null,
                project_no = x.ProductionWorkOrder != null && x.ProductionWorkOrder.ReleaseProductionOrderDetail != null
                    ? x.ProductionWorkOrder.ReleaseProductionOrderDetail.ProjectNo
                    : null,
                project_name = x.ProductionWorkOrder != null && x.ProductionWorkOrder.ReleaseProductionOrderDetail != null && x.ProductionWorkOrder.ReleaseProductionOrderDetail.ProjectMaster != null
                    ? x.ProductionWorkOrder.ReleaseProductionOrderDetail.ProjectMaster.ProjectName
                    : null,
                weight = x.ProductionWorkOrder != null && x.ProductionWorkOrder.ReleaseProductionOrderDetail != null
                    ? x.ProductionWorkOrder.ReleaseProductionOrderDetail.Weight
                    : null,
                created_at = x.CreatedAt
            })
            .ToListAsync();
        return ApiOk(logs);
    }

    [HttpGet("activity-logs/export")]
    public async Task<IActionResult> ExportActivityLogs(
        [FromQuery] int? workOrderId,
        [FromQuery] DateTime? date,
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate)
    {
        var range = ResolveActivityLogDateRange(date, startDate, endDate, defaultToLastSevenDays: true);
        var logs = await ApplyActivityLogFilters(ActivityLogReportQuery(), workOrderId, range.Start, range.EndExclusive)
            .OrderBy(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .Take(10000)
            .ToListAsync();

        var rows = logs.Select(ToActivityReportRow).ToList();
        var exporter = new ProductionActivityExcelExporter();
        var fileContent = exporter.Export(rows, new ProductionActivityReportContext(
            range.DisplayStart,
            range.DisplayEnd,
            DateTime.Now));
        var fileName = $"Production-Activity-{DateTime.Now:yyyyMMdd-HHmm}.xlsx";

        return File(
            fileContent,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }

    private IQueryable<ProductionActivityLog> ActivityLogReportQuery() =>
        _db.ProductionActivityLogs.AsNoTracking()
            .Include(x => x.ProductionWorkOrder!)
                .ThenInclude(x => x.ReleaseProductionOrderDetail)
                    .ThenInclude(x => x!.ProjectMaster)
            .Include(x => x.User);

    private static IQueryable<ProductionActivityLog> ApplyActivityLogFilters(
        IQueryable<ProductionActivityLog> query,
        int? workOrderId,
        DateTime? start,
        DateTime? endExclusive)
    {
        if (workOrderId.HasValue)
        {
            query = query.Where(x => x.ProductionWorkOrderId == workOrderId.Value);
        }

        if (start.HasValue)
        {
            query = query.Where(x => x.CreatedAt >= start.Value);
        }

        if (endExclusive.HasValue)
        {
            query = query.Where(x => x.CreatedAt < endExclusive.Value);
        }

        return query;
    }

    private static ActivityLogDateRange ResolveActivityLogDateRange(
        DateTime? date,
        DateTime? startDate,
        DateTime? endDate,
        bool defaultToLastSevenDays)
    {
        var now = DateTime.Now;

        if (date.HasValue)
        {
            var selectedDate = date.Value.Date;
            return new ActivityLogDateRange(
                selectedDate,
                ResolveEndExclusive(selectedDate, now),
                selectedDate,
                selectedDate);
        }

        var start = startDate?.Date;
        var endDisplay = endDate?.Date;

        if (!start.HasValue && !endDisplay.HasValue && defaultToLastSevenDays)
        {
            start = now.Date.AddDays(-7);
            endDisplay = now.Date;
        }

        if (start.HasValue && endDisplay.HasValue && endDisplay.Value < start.Value)
        {
            (start, endDisplay) = (endDisplay, start);
        }

        return new ActivityLogDateRange(
            start,
            endDisplay.HasValue ? ResolveEndExclusive(endDisplay.Value, now) : null,
            start,
            endDisplay);
    }

    private static DateTime ResolveEndExclusive(DateTime endDate, DateTime now) =>
        endDate.Date >= now.Date ? now.AddSeconds(1) : endDate.Date.AddDays(1);

    private static ProductionActivityReportRow ToActivityReportRow(ProductionActivityLog log)
    {
        var order = log.ProductionWorkOrder;
        var rpo = order?.ReleaseProductionOrderDetail;
        var user = log.User;

        return new ProductionActivityReportRow(
            log.CreatedAt,
            order?.OrderNumber ?? $"Order #{log.ProductionWorkOrderId}",
            rpo?.LotNo ?? "-",
            rpo?.ProjectNo ?? "-",
            rpo?.ProjectMaster?.ProjectName ?? "-",
            rpo?.Weight,
            user?.FullName ?? user?.Username ?? "System",
            log.ActivityType.ToString().Replace("_", " "),
            log.Remarks ?? "-");
    }

    private sealed record ActivityLogDateRange(
        DateTime? Start,
        DateTime? EndExclusive,
        DateTime? DisplayStart,
        DateTime? DisplayEnd);

    private IQueryable<ProductionWorkOrder> WorkOrderQuery() =>
        _db.ProductionWorkOrders.AsNoTracking()
            .Include(x => x.ShiftMaster)
            .Include(x => x.AreaMaster)
            .Include(x => x.ReleaseProductionOrderDetail)
                .ThenInclude(x => x!.ProjectMaster)
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
            .Include(x => x.ShiftMaster)
            .Include(x => x.AreaMaster)
            .Include(x => x.ReleaseProductionOrderDetail)
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
        await _db.Entry(order).Reference(x => x.ShiftMaster).LoadAsync();
        await _db.Entry(order).Reference(x => x.AreaMaster).LoadAsync();
        await _db.Entry(order).Reference(x => x.ReleaseProductionOrderDetail).LoadAsync();
        _db.Entry(order).Collection(x => x.Operators).IsLoaded = false;
        await _db.Entry(order).Collection(x => x.Operators).LoadAsync();
        foreach (var workOrderOperator in order.Operators)
        {
            await _db.Entry(workOrderOperator).Reference(x => x.PicCard).LoadAsync();
            await _db.Entry(workOrderOperator).Reference(x => x.ShiftMaster).LoadAsync();
        }
    }

    private void AddLog(int workOrderId, ProductionActivityType activityType, string? remarks)
    {
        _db.ProductionActivityLogs.Add(new ProductionActivityLog
        {
            ProductionWorkOrderId = workOrderId,
            UserId = GetCurrentUserId(),
            ActivityType = activityType,
            Remarks = remarks,
            CreatedAt = DateTime.Now
        });
    }

    private int? GetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(value, out var userId) ? userId : null;
    }

    private static ProductionIntegrationSettingResponse ToSettingResponse(ProductionIntegrationSetting setting) =>
        new()
        {
            SettingKey = setting.SettingKey,
            BaseUrl = setting.BaseUrl,
            EndpointPath = setting.EndpointPath,
            Username = setting.Username,
            PasswordSet = !string.IsNullOrWhiteSpace(setting.Password),
            FilterFieldName = setting.FilterFieldName,
            Top = setting.Top,
            Skip = setting.Skip,
            IsActive = setting.IsActive
        };

    private static string? NormalizeBaseUrl(string? value)
    {
        var normalized = NormalizeText(value)?.TrimEnd('/');
        return normalized;
    }

    private static string NormalizeEndpointPath(string? value, string defaultEndpointPath = "/fab-shiage-prod-res/")
    {
        var normalized = NormalizeText(value) ?? defaultEndpointPath;
        return normalized.StartsWith('/') ? normalized : $"/{normalized}";
    }

    private static string NormalizeSettingKey(string? value)
    {
        var normalized = NormalizeText(value)?.ToLowerInvariant();
        if (normalized is null || normalized.Any(x => !char.IsLetterOrDigit(x) && x != '_' && x != '-'))
        {
            throw new ArgumentException("Setting key is invalid.");
        }

        return normalized;
    }

    private static string GetDefaultEndpointPath(string settingKey) => settingKey switch
    {
        ShiageLotNoSettingKey => "/fab-shiage-prod-res/",
        InternalSystemAuthSettingKey => "/auth/login",
        InternalSystemRefreshSettingKey => "/auth/refresh",
        _ => "/"
    };

    private static string GetDefaultFilterFieldName(string settingKey) => settingKey switch
    {
        ShiageLotNoSettingKey => "LOT_NO",
        _ => "-"
    };

    private static string NormalizeLineCode(string? value)
    {
        var normalized = NormalizeText(value) ?? "-";
        return normalized.Length <= 50 ? normalized : normalized[..50];
    }

    private async Task<ProductionIntegrationSetting> GetShiageLotNoSettingEntity()
    {
        return await GetIntegrationSettingEntity(ShiageLotNoSettingKey);
    }

    private async Task<ProductionIntegrationSetting> GetIntegrationSettingEntity(string settingKey)
    {
        var setting = await _db.ProductionIntegrationSettings.AsNoTracking()
            .FirstOrDefaultAsync(x => x.SettingKey == settingKey);

        return setting ?? new ProductionIntegrationSetting
        {
            SettingKey = settingKey,
            BaseUrl = string.Empty,
            EndpointPath = GetDefaultEndpointPath(settingKey),
            FilterFieldName = GetDefaultFilterFieldName(settingKey),
            Top = 1,
            Skip = 0,
            IsActive = false
        };
    }

    private async Task<ShiageLotNoResult?> FetchShiageLotNo(string lotNo)
    {
        var setting = await GetShiageLotNoSettingEntity();
        if (!setting.IsActive || string.IsNullOrWhiteSpace(setting.BaseUrl))
        {
            return null;
        }

        var url = BuildShiageUrl(setting, lotNo);
        using var response = await SendShiageRequest(url);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(BuildShiageErrorMessage(url, response, content));
        }

        using var document = JsonDocument.Parse(content);
        var data = ResolveShiageDataElement(document.RootElement);
        return data.HasValue ? ToShiageLotNoResult(data.Value) : null;
    }

    private static string BuildShiageUrl(ProductionIntegrationSetting setting, string? lotNo)
    {
        var baseUrl = NormalizeBaseUrl(setting.BaseUrl) ?? string.Empty;
        var endpointPath = NormalizeEndpointPath(setting.EndpointPath);
        var filterFieldName = NormalizeText(setting.FilterFieldName) ?? "LOT_NO";
        var query = new Dictionary<string, string>();

        if (!string.IsNullOrWhiteSpace(lotNo))
        {
            var filterValue = lotNo.Replace("'", "''");
            query["$filter"] = $"{filterFieldName} eq '{filterValue}'";
        }

        var queryString = string.Join("&", query.Select(x => $"{Uri.EscapeDataString(x.Key)}={Uri.EscapeDataString(x.Value)}"));

        return string.IsNullOrWhiteSpace(queryString)
            ? $"{baseUrl}{endpointPath}"
            : $"{baseUrl}{endpointPath}?{queryString}";
    }

    private static string BuildShiageErrorMessage(string url, HttpResponseMessage response, string content)
    {
        var preview = string.IsNullOrWhiteSpace(content)
            ? response.ReasonPhrase ?? "No response body."
            : content.Trim();
        if (preview.Length > 500)
        {
            preview = $"{preview[..500]}...";
        }

        return $"Shiage endpoint failed with status {(int)response.StatusCode}. URL: {url}. Response: {preview}";
    }

    private async Task<HttpResponseMessage> SendShiageRequest(string url)
    {
        var client = _httpClientFactory.CreateClient();
        var token = await GetExternalAccessToken();
        var response = await client.SendAsync(CreateShiageRequest(url, token));
        if ((int)response.StatusCode != 401)
        {
            return response;
        }

        response.Dispose();
        token = await GetExternalAccessToken(forceRefresh: true);
        return await client.SendAsync(CreateShiageRequest(url, token));
    }

    private static HttpRequestMessage CreateShiageRequest(string url, string? token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return request;
    }

    private async Task<string?> GetExternalAccessToken(bool forceRefresh = false)
    {
        var authSetting = await GetIntegrationSettingEntity(InternalSystemAuthSettingKey);
        if (!authSetting.IsActive)
        {
            return null;
        }

        var baseUrl = NormalizeBaseUrl(authSetting.BaseUrl);
        var endpointPath = NormalizeEndpointPath(authSetting.EndpointPath, GetDefaultEndpointPath(InternalSystemAuthSettingKey));
        var username = NormalizeText(authSetting.Username);
        var password = NormalizeText(authSetting.Password);

        if (baseUrl is null || username is null || password is null)
        {
            return null;
        }

        var cacheKey = $"{baseUrl}|{username}";
        if (!forceRefresh && CachedExternalToken is { } cachedToken &&
            cachedToken.CacheKey == cacheKey &&
            cachedToken.ExpiresAt > DateTimeOffset.Now.AddMinutes(1))
        {
            return cachedToken.AccessToken;
        }

        await ExternalTokenLock.WaitAsync();
        try
        {
            if (!forceRefresh && CachedExternalToken is { } lockedToken &&
                lockedToken.CacheKey == cacheKey &&
                lockedToken.ExpiresAt > DateTimeOffset.Now.AddMinutes(1))
            {
                return lockedToken.AccessToken;
            }

            if (CachedExternalToken is { RefreshToken: { Length: > 0 } refreshToken } &&
                CachedExternalToken.CacheKey == cacheKey)
            {
                var refreshedToken = await RefreshExternalAccessToken(refreshToken, baseUrl, cacheKey);
                if (refreshedToken is not null)
                {
                    CachedExternalToken = refreshedToken;
                    return refreshedToken.AccessToken;
                }
            }

            var loggedInToken = await LoginExternalAccessToken(baseUrl, endpointPath, username, password, cacheKey);
            CachedExternalToken = loggedInToken;
            return loggedInToken.AccessToken;
        }
        finally
        {
            ExternalTokenLock.Release();
        }
    }

    private async Task<ExternalAuthToken> LoginExternalAccessToken(
        string baseUrl,
        string endpointPath,
        string username,
        string password,
        string cacheKey)
    {
        var url = $"{baseUrl}{endpointPath}";
        using var response = await _httpClientFactory.CreateClient().PostAsJsonAsync(url, new
        {
            email = username,
            password
        });
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Login external failed with status {(int)response.StatusCode}: {content}");
        }

        var token = GetInternalLoginToken(content);
        if (token is null)
        {
            throw new InvalidOperationException("Login external succeeded but access token was not found.");
        }

        return new ExternalAuthToken(
            token,
            GetInternalRefreshToken(content),
            ResolveTokenExpiry(token),
            cacheKey);
    }

    private async Task<ExternalAuthToken?> RefreshExternalAccessToken(string refreshToken, string authBaseUrl, string cacheKey)
    {
        var storedRefreshSetting = await GetIntegrationSettingEntity(InternalSystemRefreshSettingKey);
        if (!storedRefreshSetting.IsActive)
        {
            return null;
        }

        var refreshBaseUrl = NormalizeBaseUrl(storedRefreshSetting.BaseUrl) ?? authBaseUrl;
        var refreshEndpointPath = NormalizeEndpointPath(
            storedRefreshSetting.EndpointPath,
            storedRefreshSetting.EndpointPath is { Length: > 0 }
                ? storedRefreshSetting.EndpointPath
                : GetDefaultEndpointPath(InternalSystemRefreshSettingKey));
        var separator = refreshEndpointPath.Contains('?') ? "&" : "?";
        var refreshUrl = $"{refreshBaseUrl}{refreshEndpointPath}{separator}refresh_token={Uri.EscapeDataString(refreshToken)}";

        using var response = await _httpClientFactory.CreateClient().PostAsync(refreshUrl, null);
        var content = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var token = GetInternalLoginToken(content);
        if (token is null)
        {
            return null;
        }

        return new ExternalAuthToken(
            token,
            GetInternalRefreshToken(content) ?? refreshToken,
            ResolveTokenExpiry(token),
            cacheKey);
    }

    private static DateTimeOffset ResolveTokenExpiry(string token)
    {
        var jwtExpiry = TryGetJwtExpiry(token);
        return jwtExpiry is not null && jwtExpiry > DateTimeOffset.Now
            ? jwtExpiry.Value
            : DateTimeOffset.Now.AddMinutes(50);
    }

    private static DateTimeOffset? TryGetJwtExpiry(string token)
    {
        var parts = token.Split('.');
        if (parts.Length < 2)
        {
            return null;
        }

        try
        {
            var payload = parts[1]
                .Replace('-', '+')
                .Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.TryGetProperty("exp", out var expElement) &&
                expElement.TryGetInt64(out var exp))
            {
                return DateTimeOffset.FromUnixTimeSeconds(exp);
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    private static JsonElement? ResolveShiageDataElement(JsonElement root)
    {
        var data = TryGetProperty(root, "data") ?? root;
        if (data.ValueKind == JsonValueKind.Array)
        {
            return data.GetArrayLength() > 0 ? data[0] : null;
        }

        return data.ValueKind == JsonValueKind.Object ? data : null;
    }

    private static IEnumerable<JsonElement> ResolveShiageDataRows(JsonElement root)
    {
        var data = TryGetProperty(root, "data") ?? root;
        if (data.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in data.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object)
                {
                    yield return item;
                }
            }

            yield break;
        }

        if (data.ValueKind == JsonValueKind.Object)
        {
            yield return data;
        }
    }

    private static ShiageLotNoResult ToShiageLotNoResult(JsonElement data)
    {
        return new ShiageLotNoResult(
            ProjectNo: GetJsonString(data, "PROJECT_NO"),
            OrderNo: GetJsonString(data, "ORDER_NO"),
            LotNo: GetJsonString(data, "LOT_NO"),
            Weight: GetJsonDecimal(data, "WEIGHT"),
            ProjectName: GetJsonString(data, "PROJECT_NAME"),
            Line: GetJsonString(data, "LINE"),
            CutPlan: GetJsonDate(data, "CUT_PLAN"));
    }

    private static FabShiageProductionResultResponse ToFabShiageProductionResultResponse(JsonElement data)
    {
        return new FabShiageProductionResultResponse
        {
            ProjectNo = GetJsonString(data, "PROJECT_NO"),
            OrderNo = GetJsonString(data, "ORDER_NO"),
            LotNo = GetJsonString(data, "LOT_NO"),
            Weight = GetJsonDecimal(data, "WEIGHT"),
            ProjectName = GetJsonString(data, "PROJECT_NAME")
        };
    }

    private static JsonElement? TryGetProperty(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return property.Value;
            }
        }

        return null;
    }

    private static string? GetInternalLoginMessage(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;
            return GetJsonString(root, "message") ??
                GetJsonString(root, "error") ??
                GetJsonString(root, "statusText") ??
                GetJsonString(root, "status");
        }
        catch
        {
            return content.Length > 300 ? content[..300] : content;
        }
    }

    private static string? GetInternalLoginToken(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.String)
            {
                return NormalizeText(root.GetString());
            }

            var data = TryGetProperty(root, "data");
            return GetJsonString(root, "token") ??
                GetJsonString(root, "access_token") ??
                GetJsonString(root, "accessToken") ??
                (data.HasValue ? GetJsonString(data.Value, "token") : null) ??
                (data.HasValue ? GetJsonString(data.Value, "access_token") : null) ??
                (data.HasValue ? GetJsonString(data.Value, "accessToken") : null);
        }
        catch
        {
            return null;
        }
    }

    private static string? GetInternalRefreshToken(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;
            var data = TryGetProperty(root, "data");
            return GetJsonString(root, "refresh_token") ??
                GetJsonString(root, "refreshToken") ??
                (data.HasValue ? GetJsonString(data.Value, "refresh_token") : null) ??
                (data.HasValue ? GetJsonString(data.Value, "refreshToken") : null);
        }
        catch
        {
            return null;
        }
    }

    private static string? MaskToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        return token.Length <= 12 ? "********" : $"{token[..6]}...{token[^6..]}";
    }

    private static string? GetJsonString(JsonElement element, string name)
    {
        var value = TryGetProperty(element, name);
        return value.HasValue && value.Value.ValueKind != JsonValueKind.Null
            ? NormalizeText(value.Value.ToString())
            : null;
    }

    private static decimal? GetJsonDecimal(JsonElement element, string name)
    {
        var value = TryGetProperty(element, name);
        if (!value.HasValue || value.Value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (value.Value.ValueKind == JsonValueKind.Number && value.Value.TryGetDecimal(out var number))
        {
            return number;
        }

        return decimal.TryParse(value.Value.ToString(), out var parsed) ? parsed : null;
    }

    private static DateTime? GetJsonDate(JsonElement element, string name)
    {
        var value = GetJsonString(element, name);
        return DateTime.TryParse(value, out var parsed) ? parsed.Date : null;
    }

    private sealed record ShiageLotNoResult(
        string? ProjectNo,
        string? OrderNo,
        string? LotNo,
        decimal? Weight,
        string? ProjectName,
        string? Line,
        DateTime? CutPlan);

    private sealed record ExternalAuthToken(
        string AccessToken,
        string? RefreshToken,
        DateTimeOffset ExpiresAt,
        string CacheKey);

    private async Task<int?> UpsertProjectMaster(string? projectNo, string? projectName)
    {
        var normalizedProjectNo = NormalizeText(projectNo);
        if (normalizedProjectNo is null)
        {
            return null;
        }

        var normalizedProjectName = NormalizeText(projectName) ?? normalizedProjectNo;
        var now = DateTime.Now;
        var project = await _db.ProjectMasters.FirstOrDefaultAsync(x => x.ProjectNo == normalizedProjectNo);
        if (project is null)
        {
            project = new ProjectMaster
            {
                ProjectNo = normalizedProjectNo,
                CreatedAt = now
            };
            _db.ProjectMasters.Add(project);
        }

        project.ProjectName = normalizedProjectName;
        project.IsActive = true;
        project.UpdatedAt = now;

        if (project.Id == 0)
        {
            await _db.SaveChangesAsync();
        }

        return project.Id;
    }

    private async Task UpsertReleaseProductionOrderDetail(
        int workOrderId,
        string? orderNo,
        string? lotNo,
        string? projectNo,
        decimal? weight,
        string? projectName = null)
    {
        var normalizedOrderNo = NormalizeText(orderNo);
        var normalizedLotNo = NormalizeText(lotNo);
        var normalizedProjectNo = NormalizeText(projectNo);
        var normalizedWeight = weight.HasValue ? Math.Round(weight.Value, 3) : (decimal?)null;

        if (normalizedOrderNo is null &&
            normalizedLotNo is null &&
            normalizedProjectNo is null &&
            !normalizedWeight.HasValue)
        {
            return;
        }

        var detail = await _db.ReleaseProductionOrderDetails
            .FirstOrDefaultAsync(x => x.ProductionWorkOrderId == workOrderId);
        var now = DateTime.Now;

        if (detail is null)
        {
            detail = new ReleaseProductionOrderDetail
            {
                ProductionWorkOrderId = workOrderId,
                CreatedAt = now
            };
            _db.ReleaseProductionOrderDetails.Add(detail);
        }

        detail.OrderNo = normalizedOrderNo;
        detail.LotNo = normalizedLotNo;
        detail.ProjectNo = normalizedProjectNo;
        detail.ProjectMasterId = await UpsertProjectMaster(normalizedProjectNo, projectName);
        detail.Weight = normalizedWeight;
        detail.UpdatedAt = now;
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

    private Task<PicCard?> FindActivePicCard(string? operatorCode)
    {
        var normalized = NormalizeText(operatorCode);
        if (normalized is null)
        {
            return Task.FromResult<PicCard?>(null);
        }

        return _db.PicCards.FirstOrDefaultAsync(x =>
            x.IsActive &&
            (x.CardUid == normalized || x.EmployeeNo == normalized));
    }

    private static string? NormalizeText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? ExtractLotNoFromScan(string? value)
    {
        var normalized = NormalizeText(value);
        if (normalized is null)
        {
            return null;
        }

        var firstWhitespaceIndex = normalized.IndexOfAny([' ', '\t', '\r', '\n']);
        if (firstWhitespaceIndex < 0)
        {
            return normalized;
        }

        var afterPrefix = normalized[(firstWhitespaceIndex + 1)..].TrimStart();
        if (afterPrefix.Length == 0)
        {
            return normalized;
        }

        return afterPrefix.Length > 10 ? afterPrefix[..10] : afterPrefix;
    }

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

    private static CuttingListResponse ToProductionHistoryResponse(ProductionWorkOrder order)
    {
        var rpo = order.ReleaseProductionOrderDetail;
        var activeOperators = order.Operators
            .Where(x => x.IsActive && x.PicCard is not null)
            .OrderBy(x => x.ScannedAt)
            .ToList();
        List<ProductionOperatorResponse> startOperators =
            SnapshotOperatorResponses(order, ProductionOperatorSnapshotType.START, order.StartedAt);
        List<ProductionOperatorResponse> finishOperators =
            SnapshotOperatorResponses(order, ProductionOperatorSnapshotType.FINISH, order.CompletedAt);

        return new CuttingListResponse
        {
            Id = order.Id,
            LineCode = GetLineDisplay(order),
            PlannedQty = order.TargetQty,
            PlanDate = order.PlanDate,
            Status = ProductionStatusMaster.ToCuttingListStatus(order.StatusMasterId),
            CreatedAt = order.CreatedAt,
            OrderNumber = order.OrderNumber,
            StartedAt = order.StartedAt,
            CompletedAt = order.CompletedAt,
            Operators = ToOperatorResponses(order, activeOperators),
            StartOperators = startOperators,
            FinishOperators = finishOperators,
            LotNo = rpo?.LotNo,
            ProjectNo = rpo?.ProjectNo,
            ProjectName = rpo?.ProjectMaster?.ProjectName,
            Weight = rpo?.Weight
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

    private static string GetLineDisplay(ProductionWorkOrder order)
    {
        var lineNo = NormalizeText(order.AreaMaster?.AreaCode);
        var lineName = NormalizeText(order.AreaMaster?.AreaName);
        if (lineNo is null)
        {
            return order.LineCode;
        }

        return lineName is null || string.Equals(lineNo, lineName, StringComparison.OrdinalIgnoreCase)
            ? lineNo
            : $"{lineNo} - {lineName}";
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
        var primaryOperator = activeOperators.FirstOrDefault()?.PicCard;
        var rpo = order.ReleaseProductionOrderDetail;

        return new ProductionWorkOrderResponse
        {
            Id = order.Id,
            OrderNumber = order.OrderNumber,
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
            PlanDate = order.PlanDate,
            StartedAt = order.StartedAt,
            CompletedAt = order.CompletedAt,
            UpdatedAt = order.UpdatedAt,
            LotNo = rpo?.LotNo,
            ProjectNo = rpo?.ProjectNo,
            Weight = rpo?.Weight
        };
    }

    private static ProductionDashboardWorkOrderResponse ToDashboardResponse(ProductionWorkOrder order)
    {
        var rpo = order.ReleaseProductionOrderDetail;

        return new ProductionDashboardWorkOrderResponse
        {
            Id = order.Id,
            ProjectNo = rpo?.ProjectNo,
            OrderNo = rpo?.OrderNo ?? order.OrderNumber,
            LotNo = rpo?.LotNo,
            Weight = rpo?.Weight,
            Status = order.Status
        };
    }
}
