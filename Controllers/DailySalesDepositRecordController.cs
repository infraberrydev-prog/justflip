using ClosedXML.Excel;
using DocumentFormat.OpenXml.Drawing.Spreadsheet;
using JustFlip.Class;
using JustFlip.DTO;
using JustFlip.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Org.BouncyCastle.Asn1.Ocsp;
using System.Drawing;
using System.IO.Compression;
using System.Security.Claims;
using System.Text.Json;

namespace JustFlip.Controllers
{
    [Authorize] // Protektado ng JWT, kailangan naka-login ang user
    [ApiController]
    [Route("api/report")]
    public class DailySalesDepositRecordController : ControllerBase
    {
        private readonly JustFlipDbContext _context;
        private readonly IOutputCacheStore _cacheStore;

        private const string ReportsCacheTag = "reports-cache-tag";

        public DailySalesDepositRecordController(JustFlipDbContext context, IOutputCacheStore cacheStore)
        {
            _context = context;
            _cacheStore = cacheStore;
        }

        [HttpPost("save-report")]
        [EnableRateLimiting("StrictWritePolicy")]
        [ProducesResponseType(typeof(SuccessfulResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> SubmitRecord([FromBody] CreateDailySalesDepositRecordDto dto)
        {
            // 1. FluentValidation Process
            //var validator = new CreateDailySalesDepositRecordDtoValidator();
            //var validatorResult = await validator.ValidateAsync(dto);

            //if (!validatorResult.IsValid)
            //{
            //    var firstError = validatorResult.Errors.First();
            //    return BadRequest(new ErrorResponse(400, firstError.ErrorCode, firstError.ErrorMessage));
            //}

            // 2. Kuhanin ang secure BranchId mula sa JWT token ng nag-login na user
            var branchIdClaim = User.FindFirst("BranchId")?.Value;
            if (string.IsNullOrEmpty(branchIdClaim))
            {
                return Unauthorized(new ErrorResponse(401, "UNAUTHORIZED", "Branch assignment missing from user profile."));
            }

            int userBranchId = int.Parse(branchIdClaim);

            // 3. Kuhanin ang saktong BranchCode mula sa Branches table
            var branchCode = await _context.Branches
                .Where(b => b.Id == userBranchId)
                .Select(b => b.BranchCode) // O b.Code depende sa column name mo
                .FirstOrDefaultAsync();

            if (string.IsNullOrEmpty(branchCode))
            {
                return NotFound(new ErrorResponse(404, "NOT_FOUND", $"Branch with ID {userBranchId} was not found in the system."));
            }

            // 4. I-map ang Parent Record (Gaya ng nasa Supabase image mo: DailySalesDepositRecords)
            var newRecord = new DailySalesDepositRecord
            {
                BranchId = userBranchId,
                BranchCode = branchCode,
                ReportName = dto.ReportName,
                CreatedAt = DateTime.UtcNow
            };

            // 5. I-loop at i-map ang bawat Row galing sa UI (At itag ang BranchCode)
            foreach (var rowDto in dto.Rows)
            {
                newRecord.Rows.Add(new DailySalesDepositRecordRow
                {
                    BranchCode = branchCode, // 👈 Dito nasasave ang 'TNZ' o 'LNC' string sa row table!
                    CashierName = rowDto.CashierName,
                    ControlNo = rowDto.ControlNo,
                    DateOfTransaction = rowDto.DateOfTransaction,
                    TotalGrossCash = rowDto.TotalGrossCash,
                    CreditCardPayment = rowDto.CreditCardPayment,
                    SalaryAdvance = rowDto.SalaryAdvance,
                    Commission = rowDto.Commission,
                    Expenses = rowDto.Expenses,
                    TotalExpenses = rowDto.TotalExpenses,
                    AmountDeposited = rowDto.AmountDeposited,
                    Remarks = rowDto.Remarks
                });
            }

            // 6. I-save sa PostgreSQL Database
            _context.DailySalesDepositRecords.Add(newRecord);
            await _context.SaveChangesAsync();

            return Ok(new SuccessfulResponse { message = "Daily sales deposit record successfully created and tagged!" });
        }

        [HttpGet("get-all-reports")]
        [OutputCache(PolicyName = "ReportsListCache", Tags = [ReportsCacheTag])]
        [ProducesResponseType(typeof(PagedReportResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetRecords(
            [FromQuery] string? search,
            [FromQuery] DateOnly? startDateCreated,
            [FromQuery] DateOnly? endDateCreated,
            [FromQuery] decimal? minRevenue,
            [FromQuery] decimal? maxRevenue,
            [FromQuery] string? lastUpdated = "ALL", // Short codes: TOD, L7D, L30D, TM, ALL
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? sortBy = "createdAt", // Default Column
            [FromQuery] string? sortOrder = "DESC")   // Direction: ASC, DESC
        {
            try
            {
                // 1. Kuhanin ang Role at BranchId mula sa JWT Token ng user
                var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
                var branchIdClaim = User.FindFirst("BranchId")?.Value;

                if (string.IsNullOrEmpty(branchIdClaim))
                {
                    return Unauthorized(new ErrorResponse(401, "UNAUTHORIZED", "Branch configuration missing from token."));
                }

                int userBranchId = int.Parse(branchIdClaim);

                if (page < 1) page = 1;
                if (pageSize < 1) pageSize = 10;

                // 2. Base query: I-load ang records
                var query = _context.DailySalesDepositRecords.AsNoTracking();

                // 🔒 Multi-Branch Security Filter: Kung hindi Admin, sariling branch data lang ang puwedeng lumabas
                if (userRole != "Admin")
                {
                    query = query.Where(r => r.BranchId == userBranchId);
                }

                // 3. Search Filter (by Report Name)
                if (!string.IsNullOrWhiteSpace(search))
                {
                    query = query.Where(r => r.ReportName.ToLower().Contains(search.ToLower()));
                }

                // 4. Date Created Range Filter
                if (startDateCreated.HasValue)
                    query = query.Where(r => r.CreatedAt >= startDateCreated.Value.ToDateTime(TimeOnly.MinValue));

                if (endDateCreated.HasValue)
                    query = query.Where(r => r.CreatedAt <= endDateCreated.Value.ToDateTime(TimeOnly.MaxValue));

                // 5. Min/Max Revenue Filter (Total Gross Cash per Report)
                if (minRevenue.HasValue)
                    query = query.Where(r => r.Rows.Sum(row => (decimal?)row.TotalGrossCash) >= minRevenue.Value);

                if (maxRevenue.HasValue)
                    query = query.Where(r => r.Rows.Sum(row => (decimal?)row.TotalGrossCash) <= maxRevenue.Value);

                // 6. Last Updated Filter (Short Codes)
                if (!string.IsNullOrWhiteSpace(lastUpdated) && !lastUpdated.Equals("ALL", StringComparison.OrdinalIgnoreCase))
                {
                    var nowUtc = DateTime.UtcNow;

                    query = lastUpdated.ToUpper() switch
                    {
                        "TOD" => query.Where(r => r.LastDateUpdated.HasValue && r.LastDateUpdated.Value.Date == nowUtc.Date),
                        "L7D" => query.Where(r => r.LastDateUpdated.HasValue && r.LastDateUpdated.Value >= nowUtc.AddDays(-7)),
                        "L30D" => query.Where(r => r.LastDateUpdated.HasValue && r.LastDateUpdated.Value >= nowUtc.AddDays(-30)),
                        "TM" => query.Where(r => r.LastDateUpdated.HasValue &&
                                                  r.LastDateUpdated.Value.Year == nowUtc.Year &&
                                                  r.LastDateUpdated.Value.Month == nowUtc.Month),
                        _ => query
                    };
                }

                int totalRecords = await query.CountAsync();

                // 7. Sorting Logic (Aligned 1:1 sa UI Dropdown Options)
                bool isDesc = string.Equals(sortOrder, "DESC", StringComparison.OrdinalIgnoreCase);

                query = sortBy?.ToLower() switch
                {
                    "totalgross" or "gross" => isDesc
                        ? query.OrderByDescending(r => r.Rows.Sum(row => (decimal?)row.TotalGrossCash) ?? 0)
                        : query.OrderBy(r => r.Rows.Sum(row => (decimal?)row.TotalGrossCash) ?? 0),

                    "amountdeposited" or "deposited" => isDesc
                        ? query.OrderByDescending(r => r.Rows.Sum(row => (decimal?)row.AmountDeposited) ?? 0)
                        : query.OrderBy(r => r.Rows.Sum(row => (decimal?)row.AmountDeposited) ?? 0),

                    "totalexpenses" => isDesc
                        ? query.OrderByDescending(r => r.Rows.Sum(row => (decimal?)row.TotalExpenses) ?? 0)
                        : query.OrderBy(r => r.Rows.Sum(row => (decimal?)row.TotalExpenses) ?? 0),

                    "creditcard" => isDesc
                        ? query.OrderByDescending(r => r.Rows.Sum(row => (decimal?)row.CreditCardPayment) ?? 0)
                        : query.OrderBy(r => r.Rows.Sum(row => (decimal?)row.CreditCardPayment) ?? 0),

                    "salaryadvance" => isDesc
                        ? query.OrderByDescending(r => r.Rows.Sum(row => (decimal?)row.SalaryAdvance) ?? 0)
                        : query.OrderBy(r => r.Rows.Sum(row => (decimal?)row.SalaryAdvance) ?? 0),

                    "commission" => isDesc
                        ? query.OrderByDescending(r => r.Rows.Sum(row => (decimal?)row.Commission) ?? 0)
                        : query.OrderBy(r => r.Rows.Sum(row => (decimal?)row.Commission) ?? 0),

                    "expenses" => isDesc
                        ? query.OrderByDescending(r => r.Rows.Sum(row => (decimal?)row.Expenses) ?? 0)
                        : query.OrderBy(r => r.Rows.Sum(row => (decimal?)row.Expenses) ?? 0),

                    "reportname" or "name" => isDesc
                        ? query.OrderByDescending(r => r.ReportName)
                        : query.OrderBy(r => r.ReportName),

                    "lastdateupdated" or "lastupdated" => isDesc
                        ? query.OrderByDescending(r => r.LastDateUpdated ?? r.CreatedAt)
                        : query.OrderBy(r => r.LastDateUpdated ?? r.CreatedAt),

                    _ => isDesc // Default: CreatedAt
                        ? query.OrderByDescending(r => r.CreatedAt)
                        : query.OrderBy(r => r.CreatedAt)
                };

                // 8. Pagination at Mapping sa Final Response DTO
                var records = await query
                    .Skip((page - 1) * pageSize) // 👈 Naayos na ang Skip expression
                    .Take(pageSize)
                    .Select(r => new DailySalesReportListDto(
                        r.Id,
                        $"DS{r.Id:D5}",
                        r.ReportName,
                        r.CreatedAt.ToString("MMM dd, yyyy"),
                        r.Rows.Sum(row => row.TotalGrossCash),
                        r.Rows.Sum(row => row.CreditCardPayment ?? 0),
                        r.Rows.Sum(row => row.SalaryAdvance ?? 0),
                        r.Rows.Sum(row => row.Commission),
                        r.Rows.Sum(row => row.Expenses ?? 0),
                        r.Rows.Sum(row => row.TotalExpenses),
                        r.Rows.Sum(row => row.AmountDeposited)
                    ))
                    .ToListAsync();

                int totalPages = (int)Math.Ceiling((double)totalRecords / pageSize);

                return Ok(new PagedReportResponse(records, totalRecords, page, totalPages == 0 ? 1 : totalPages, pageSize));
            }
            catch (Exception)
            {
                return StatusCode(500, new ErrorResponse(500, "INTERNAL_SERVER_ERROR", "An error occurred while fetching deposit records."));
            }
        }

        [HttpGet("{id:int}/details")]
        [OutputCache(Duration = 300, Tags = [ReportsCacheTag])]
        [ProducesResponseType(typeof(DailySalesDepositRecordDetailDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetRecordDetailsById(int id)
        {
            try
            {
                // 1. Kuhanin ang Role at BranchId para sa security cross-checking
                var userRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
                var branchIdClaim = User.FindFirst("BranchId")?.Value;
                int userBranchId = string.IsNullOrEmpty(branchIdClaim) ? 0 : int.Parse(branchIdClaim);

                // 2. Hanapin ang Record at I-INCLUDE ang mga kaukulang Rows nito
                var record = await _context.DailySalesDepositRecords
                    .Include(r => r.Rows)
                    .FirstOrDefaultAsync(r => r.Id == id);

                if (record == null)
                {
                    return NotFound(new { message = "The requested report does not exist." });
                }

                // 3. SECURITY CHECK: Siguraduhin na hindi ma-pe-peek ng ibang branch ang data ng iba
                if (userRole != "Admin" && record.BranchId != userBranchId)
                {
                    return StatusCode(403, new ErrorResponse(403, "FORBIDDEN", "You are not authorized to view reports from other branches."));
                }

                // 4. I-map ang nakuha nating Data papunta sa Detailed DTO
                var detailDto = new DailySalesDepositRecordDetailDto
                {
                    Id = record.Id,
                    ReportName = record.ReportName,
                    CreatedAt = record.CreatedAt,
                    Rows = record.Rows.Select(row => new SalesRecordRowDetailDto
                    {
                        Id = row.Id,
                        CashierName = row.CashierName,
                        DateOfTransaction = row.DateOfTransaction,
                        TotalGrossCash = row.TotalGrossCash,
                        CreditCardPayment = row.CreditCardPayment,
                        AmountDeposited = row.AmountDeposited,
                        SalaryAdvance = row.SalaryAdvance,
                        Commission = row.Commission,
                        Expenses = row.Expenses,
                        TotalExpenses = row.TotalExpenses,
                        Remarks = row.Remarks
                    }).ToList()
                };

                return Ok(detailDto);
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "An error occurred while retrieving report details." });
            }
        }

        [HttpPost("rows/update/{rowId:int}")]
        [EnableRateLimiting("StrictWritePolicy")]
        [ProducesResponseType(typeof(SuccessfulResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateSingleRow(int rowId, [FromBody] UpdateEndDayReportRowDto request)
        {
            try
            {
                var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
                var branchIdClaim = User.FindFirst("BranchId")?.Value;
                var modifiedBy = User.FindFirst(ClaimTypes.Name)?.Value
                              ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                              ?? "System";

                if (string.IsNullOrEmpty(branchIdClaim))
                {
                    return Unauthorized(new ErrorResponse(401, "UNAUTHORIZED", "Branch assignment missing from authentication token."));
                }

                int userBranchId = int.Parse(branchIdClaim);

                var row = await _context.EndDayReportRows
                    .Include(r => r.EndDayReport)
                    .FirstOrDefaultAsync(r => r.Id == rowId);

                if (row == null)
                {
                    return NotFound(new ErrorResponse(404, "NOT_FOUND", $"End day report row with ID {rowId} was not found."));
                }

                if (userRole != "Admin" && row.EndDayReport?.BranchId != userBranchId)
                {
                    return StatusCode(403, new ErrorResponse(403, "FORBIDDEN", "You are not authorized to modify records from another branch."));
                }

                // 📝 Capture Old Values bago i-mutate ang entity
                var oldValuesObj = new
                {
                    row.EmployeeName,
                    row.GrossAmount,
                    row.CreditCardPayment,
                    row.Commission,
                    row.SalonExpenses,
                    row.SalaryAdvance
                };

                // Track changed columns
                var changedCols = new List<string>();
                if (row.EmployeeName != request.EmployeeName) changedCols.Add("EmployeeName");
                if (row.GrossAmount != request.GrossAmount) changedCols.Add("GrossAmount");
                if (row.CreditCardPayment != request.CreditCardPayment) changedCols.Add("CreditCardPayment");
                if (row.Commission != request.Commission) changedCols.Add("Commission");
                if (row.SalonExpenses != request.SalonExpenses) changedCols.Add("SalonExpenses");
                if (row.SalaryAdvance != request.SalaryAdvance) changedCols.Add("SalaryAdvance");

                // Apply Updates
                row.EmployeeName = request.EmployeeName;
                row.GrossAmount = request.GrossAmount;
                row.CreditCardPayment = request.CreditCardPayment;
                row.Commission = request.Commission;
                row.SalonExpenses = request.SalonExpenses;
                row.SalaryAdvance = request.SalaryAdvance;

                if (row.EndDayReport != null)
                {
                    row.EndDayReport.LastDateUpdated = DateTime.UtcNow;
                }

                // Capture New Values
                var newValuesObj = new
                {
                    row.EmployeeName,
                    row.GrossAmount,
                    row.CreditCardPayment,
                    row.Commission,
                    row.SalonExpenses,
                    row.SalaryAdvance
                };

                // 🛡️ Create AuditLog Record
                var auditLog = new AuditLogs
                {
                    EntityName = nameof(EndDayReportRow),
                    EntityId = row.Id,
                    Action = "UPDATE",
                    ModifiedBy = modifiedBy,
                    Timestamp = DateTime.UtcNow,
                    OldValues = JsonSerializer.Serialize(oldValuesObj),
                    NewValues = JsonSerializer.Serialize(newValuesObj),
                    ChangedColumns = changedCols.Any() ? string.Join(", ", changedCols) : null
                };

                _context.AuditLogs.Add(auditLog);
                await _context.SaveChangesAsync();

                return Ok(new SuccessfulResponse { message = $"Successfully updated row {rowId}!" });
            }
            catch (Exception)
            {
                return StatusCode(500, new ErrorResponse(500, "INTERNAL_SERVER_ERROR", "An error occurred while updating the report row."));
            }
        }

        [HttpPost("delete/{id:int}")]
        [EnableRateLimiting("StrictWritePolicy")]
        [ProducesResponseType(typeof(SuccessfulResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteParentRecord(int id, [FromQuery] string? reason)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var userRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
                var branchIdClaim = User.FindFirst("BranchId")?.Value;
                var modifiedBy = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value
                              ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                              ?? "System";

                if (string.IsNullOrEmpty(branchIdClaim))
                {
                    return Unauthorized(new ErrorResponse(401, "UNAUTHORIZED", "Branch assignment missing from token."));
                }

                int userBranchId = int.Parse(branchIdClaim);
                var userBranchCode = await _context.Branches
                    .Where(r => r.Id == userBranchId)
                    .Select(b => b.BranchCode)
                    .FirstOrDefaultAsync();

                // 1. Fetch parent record kasama ang active rows
                var parentRecord = await _context.DailySalesDepositRecords
                    .Include(p => p.Rows)
                    .FirstOrDefaultAsync(p => p.Id == id);

                if (parentRecord == null)
                {
                    return NotFound(new ErrorResponse(404, "NOT_FOUND", "The parent sales deposit report you are trying to delete does not exist."));
                }

                // 2. Authorization Check
                if (userRole != "Admin" && parentRecord.BranchCode != userBranchCode)
                {
                    return StatusCode(403, new ErrorResponse(403, "FORBIDDEN", "You are not authorized to delete reports belonging to another branch."));
                }

                // Options para i-ignore ang circular references
                var jsonSerializerOptions = new System.Text.Json.JsonSerializerOptions
                {
                    ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles
                };

                // 3. Move all child rows to DeletedDailySalesDepositRecordRows
                foreach (var childRow in parentRecord.Rows)
                {
                    var archivedChild = new DeletedDailySalesDepositRecordRow
                    {
                        OriginalRowId = childRow.Id,
                        DailySalesDepositRecordId = childRow.DailySalesDepositRecordId,
                        BranchCode = childRow.BranchCode,
                        CashierName = childRow.CashierName,
                        ControlNo = childRow.ControlNo,
                        DateOfTransaction = childRow.DateOfTransaction,
                        TotalGrossCash = childRow.TotalGrossCash,
                        CreditCardPayment = childRow.CreditCardPayment,
                        SalaryAdvance = childRow.SalaryAdvance,
                        Commission = childRow.Commission,
                        Expenses = childRow.Expenses,
                        TotalExpenses = childRow.TotalExpenses,
                        AmountDeposited = childRow.AmountDeposited,
                        Remarks = childRow.Remarks,
                        DeletedAt = DateTime.UtcNow,
                        DeletedBy = modifiedBy,
                        DeletionReason = reason ?? "Parent report deleted"
                    };

                    _context.DeletedDailySalesDepositRecordRows.Add(archivedChild);
                }

                // 4. Map parent values to Replica Model
                var archivedParent = new DeletedDailySalesDepositRecord
                {
                    OriginalRecordId = parentRecord.Id,
                    BranchId = parentRecord.BranchId,
                    BranchCode = parentRecord.BranchCode,
                    ReportName = parentRecord.ReportName,
                    OriginalCreatedAt = parentRecord.CreatedAt,
                    LastDateUpdated = parentRecord.LastDateUpdated,
                    DeletedAt = DateTime.UtcNow,
                    DeletedBy = modifiedBy,
                    DeletionReason = reason ?? "User soft-deleted parent report",
                    ArchivedRowsJson = System.Text.Json.JsonSerializer.Serialize(parentRecord.Rows, jsonSerializerOptions)
                };

                // 5. Create Audit Log
                var auditLog = new AuditLogs
                {
                    EntityName = nameof(DailySalesDepositRecord),
                    EntityId = parentRecord.Id,
                    Action = "SOFT_DELETE_ARCHIVE_PARENT_REPORT",
                    ModifiedBy = modifiedBy,
                    Timestamp = DateTime.UtcNow,
                    OldValues = System.Text.Json.JsonSerializer.Serialize(parentRecord, jsonSerializerOptions),
                    NewValues = System.Text.Json.JsonSerializer.Serialize(archivedParent, jsonSerializerOptions),
                    ChangedColumns = "Moved parent record to DeletedDailySalesDepositRecords and rows to DeletedDailySalesDepositRecordRows"
                };

                // 6. DB Operations
                _context.DeletedDailySalesDepositRecords.Add(archivedParent);
                _context.DailySalesDepositRecordRows.RemoveRange(parentRecord.Rows);
                _context.DailySalesDepositRecords.Remove(parentRecord);
                _context.AuditLogs.Add(auditLog);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new SuccessfulResponse { message = $"Successfully soft-deleted and archived parent report {id} along with its rows!" });
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new ErrorResponse(500, "INTERNAL_SERVER_ERROR", "An internal error occurred while executing the parent report deletion sequence."));
            }
        }

        [HttpPost("rows/delete/{rowId:int}")]
        [EnableRateLimiting("StrictWritePolicy")]
        [ProducesResponseType(typeof(SuccessfulResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> DeleteRecord(int rowId, [FromQuery] string? reason)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var userRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
                var branchIdClaim = User.FindFirst("BranchId")?.Value;
                var modifiedBy = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value
                              ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                              ?? "System";

                if (string.IsNullOrEmpty(branchIdClaim))
                {
                    return Unauthorized(new ErrorResponse(401, "UNAUTHORIZED", "Branch assignment missing from token."));
                }

                int userBranchId = int.Parse(branchIdClaim);
                var userBranchCode = await _context.Branches
                    .Where(r => r.Id == userBranchId)
                    .Select(b => b.BranchCode)
                    .FirstOrDefaultAsync();

                // 1. Fetch record mula sa active table
                var record = await _context.DailySalesDepositRecordRows
                    .Include(r => r.DailySalesDepositRecord)
                    .FirstOrDefaultAsync(r => r.Id == rowId);

                if (record == null)
                {
                    return NotFound(new ErrorResponse(404, "NOT_FOUND", "The row you are trying to delete does not exist."));
                }

                // 2. Authorization Check
                if (userRole != "Admin" && record.BranchCode != userBranchCode)
                {
                    return StatusCode(403, new ErrorResponse(403, "FORBIDDEN", "You are not authorized to modify records belonging to another branch."));
                }

                // 3. Map values papunta sa Replica Model (DeletedDailySalesDepositRecordRow)
                var archivedRow = new DeletedDailySalesDepositRecordRow
                {
                    OriginalRowId = record.Id,
                    DailySalesDepositRecordId = record.DailySalesDepositRecordId,
                    BranchCode = record.BranchCode,
                    CashierName = record.CashierName,
                    ControlNo = record.ControlNo,
                    DateOfTransaction = record.DateOfTransaction,
                    TotalGrossCash = record.TotalGrossCash,
                    CreditCardPayment = record.CreditCardPayment,
                    SalaryAdvance = record.SalaryAdvance,
                    Commission = record.Commission,
                    Expenses = record.Expenses,
                    TotalExpenses = record.TotalExpenses,
                    AmountDeposited = record.AmountDeposited,
                    Remarks = record.Remarks,
                    DeletedAt = DateTime.UtcNow,
                    DeletedBy = modifiedBy,
                    DeletionReason = reason ?? "User requested row soft deletion"
                };

                // 4. Update parent timestamp
                if (record.DailySalesDepositRecord != null)
                {
                    record.DailySalesDepositRecord.LastDateUpdated = DateTime.UtcNow;
                }

                // 5. Create Audit Log
                var auditLog = new AuditLogs
                {
                    EntityName = nameof(DailySalesDepositRecordRow),
                    EntityId = record.Id,
                    Action = "SOFT_DELETE_ARCHIVE_ROW",
                    ModifiedBy = modifiedBy,
                    Timestamp = DateTime.UtcNow,
                    OldValues = System.Text.Json.JsonSerializer.Serialize(record),
                    NewValues = System.Text.Json.JsonSerializer.Serialize(archivedRow),
                    ChangedColumns = "Moved to DeletedDailySalesDepositRecordRows"
                };

                // 6. DB Execution
                _context.DeletedDailySalesDepositRecordRows.Add(archivedRow);
                _context.DailySalesDepositRecordRows.Remove(record);
                _context.AuditLogs.Add(auditLog);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new SuccessfulResponse { message = $"Successfully soft deleted and archived row {rowId}!" });
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new ErrorResponse(500, "INTERNAL_SERVER_ERROR", "An internal error occurred while executing the delete sequence."));
            }
        }

        [HttpPost("{reportId:int}/rows/add")]
        [EnableRateLimiting("StrictWritePolicy")]
        [ProducesResponseType(typeof(AddSingleRowResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> AddSingleRowToReport(int reportId, [FromBody] AddEndDayRowRequest request)
        {
            try
            {
                var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
                var branchIdClaim = User.FindFirst("BranchId")?.Value;
                var modifiedBy = User.FindFirst(ClaimTypes.Name)?.Value
                              ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                              ?? "System";

                if (string.IsNullOrEmpty(branchIdClaim))
                {
                    return Unauthorized(new ErrorResponse(401, "UNAUTHORIZED", "Branch assignment missing from token."));
                }

                int userBranchId = int.Parse(branchIdClaim);

                var parentReport = await _context.EndDayReports.FindAsync(reportId);
                if (parentReport == null)
                {
                    return NotFound(new ErrorResponse(404, "NOT_FOUND", $"The master report with ID {reportId} was not found."));
                }

                if (userRole != "Admin" && parentReport.BranchId != userBranchId)
                {
                    return StatusCode(403, new ErrorResponse(403, "FORBIDDEN", "You are not authorized to add rows to another branch's report."));
                }

                var newRow = new EndDayReportRow
                {
                    BranchCode = parentReport.BranchCode,
                    EndDayReportId = reportId,
                    EmployeeName = request.EmployeeName,
                    GrossAmount = request.GrossAmount,
                    CreditCardPayment = request.CreditCardPayment,
                    Commission = request.Commission,
                    SalonExpenses = request.SalonExpenses,
                    SalaryAdvance = request.SalaryAdvance
                };

                parentReport.LastDateUpdated = DateTime.UtcNow;

                _context.EndDayReportRows.Add(newRow);
                await _context.SaveChangesAsync(); // Commit muna para makuha ang generated newRow.Id

                // 🛡️ Create AuditLog Record for CREATE Action
                var auditLog = new AuditLogs
                {
                    EntityName = nameof(EndDayReportRow),
                    EntityId = newRow.Id,
                    Action = "CREATE",
                    ModifiedBy = modifiedBy,
                    Timestamp = DateTime.UtcNow,
                    OldValues = null,
                    NewValues = JsonSerializer.Serialize(new
                    {
                        newRow.EndDayReportId,
                        newRow.BranchCode,
                        newRow.EmployeeName,
                        newRow.GrossAmount,
                        newRow.CreditCardPayment,
                        newRow.Commission,
                        newRow.SalonExpenses,
                        newRow.SalaryAdvance
                    }),
                    ChangedColumns = "EmployeeName, GrossAmount, CreditCardPayment, Commission, SalonExpenses, SalaryAdvance"
                };

                _context.AuditLogs.Add(auditLog);
                await _context.SaveChangesAsync();

                return Ok(new AddSingleRowResponse
                {
                    message = "Successfully added new row to the report!",
                    newRowId = newRow.Id
                });
            }
            catch (Exception)
            {
                return StatusCode(500, new ErrorResponse(500, "INTERNAL_SERVER_ERROR", "An error occurred while adding the row to the report."));
            }
        }

        [HttpPost("download")]
        [EnableRateLimiting("DownloadPolicy")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DownloadSalesDepositReports([FromBody] DownloadReportsRequest request)
        {
            if (request?.ReportIds == null || !request.ReportIds.Any())
            {
                return BadRequest(new ErrorResponse(400, "INVALID_REQUEST", "Please select at least one report to download."));
            }

            // 1. Multi-Branch Security Verification
            var userRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
            var branchIdClaim = User.FindFirst("BranchId")?.Value;

            if (string.IsNullOrEmpty(branchIdClaim))
            {
                return Unauthorized(new ErrorResponse(401, "UNAUTHORIZED", "Branch assignment missing from authentication token."));
            }

            int userBranchId = int.Parse(branchIdClaim);

            // 2. Fetch Reports & Filter sa BranchId ng User (kung hindi Admin)
            var query = _context.DailySalesDepositRecords
                .Include(r => r.Rows)
                .Where(r => request.ReportIds.Contains(r.Id));

            if (userRole != "Admin")
            {
                query = query.Where(r => r.BranchId == userBranchId);
            }

            var reports = await query.ToListAsync();

            if (!reports.Any())
            {
                return NotFound(new ErrorResponse(404, "NOT_FOUND", "No matching reports found for the selected IDs or you do not have permission to access them."));
            }

            // 3. KAPAG ISA LANG ANG SINELECT
            if (reports.Count == 1)
            {
                var report = reports.First();
                var fileBytes = GenerateReportExcelBytes(report);
                string fileName = $"{SanitizeFileName(report.ReportName)}.xlsx";

                return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }

            // 4. KAPAG MULTIPLE ANG SINELECT (ZIP Download)
            using (var zipStream = new MemoryStream())
            {
                using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, true))
                {
                    foreach (var report in reports)
                    {
                        var fileBytes = GenerateReportExcelBytes(report);
                        var entryName = $"{SanitizeFileName(report.ReportName)}_{report.Id}.xlsx";

                        var entry = archive.CreateEntry(entryName, CompressionLevel.Fastest);
                        using (var entryStream = entry.Open())
                        {
                            await entryStream.WriteAsync(fileBytes, 0, fileBytes.Length);
                        }
                    }
                }

                zipStream.Position = 0;
                string zipFileName = $"JustFlip_Daily_Sales_Deposit_Reports_{DateTime.UtcNow:yyyyMMdd_HHmmss}.zip";

                return File(zipStream.ToArray(), "application/zip", zipFileName);
            }
        }

        // ==========================================
        // HELPER METHODS FOR EXCEL GENERATION
        // ==========================================
        private byte[] GenerateReportExcelBytes(DailySalesDepositRecord report)
        {
            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Report Details");

                // 1. Report Title and Metadata Header (mula sa UI: Title & Subtitle/Timestamp)
                worksheet.Cell(1, 1).Value = report.ReportName;
                worksheet.Cell(1, 1).Style.Font.Bold = true;
                worksheet.Cell(1, 1).Style.Font.FontSize = 16;

                worksheet.Cell(2, 1).Value = report.CreatedAt.ToString("MMM d, yyyy - HH:mm:ss");
                worksheet.Cell(2, 1).Style.Font.FontColor = XLColor.Gray;

                // 2. Exact Table Headers batay sa UI Layout
                string[] headers = new string[]
                {
                    "Cashier Name",
                    "Date of Transaction",
                    "Total Gross (Cash)",
                    "Credit Card Payment",
                    "Total Amount Deposited",
                    "Salary Advance",
                    "Commission",
                    "Expenses",
                    "Total Expenses",
                    "Remarks"
                };

                int headerRowIndex = 4;
                for (int col = 0; col < headers.Length; col++)
                {
                    var cell = worksheet.Cell(headerRowIndex, col + 1);
                    cell.Value = headers[col];
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#F8F9FA");
                    cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    cell.Style.Border.OutsideBorderColor = XLColor.LightGray;
                }

                // 3. Populate Rows Data (Kumporme sa UI order at formatting)
                int currentRow = 5;
                foreach (var row in report.Rows)
                {
                    // Cashier Name
                    worksheet.Cell(currentRow, 1).Value = row.CashierName;

                    // Date of Transaction (e.g., Mar 1, 2026)
                    worksheet.Cell(currentRow, 2).Value = row.DateOfTransaction.ToString("MMM d, yyyy");

                    // Total Gross (Cash)
                    worksheet.Cell(currentRow, 3).Value = row.TotalGrossCash;
                    worksheet.Cell(currentRow, 3).Style.NumberFormat.Format = "₱#,##0.00";

                    // Credit Card Payment
                    worksheet.Cell(currentRow, 4).Value = row.CreditCardPayment ?? 0;
                    worksheet.Cell(currentRow, 4).Style.NumberFormat.Format = "₱#,##0.00";

                    // Total Amount Deposited
                    worksheet.Cell(currentRow, 5).Value = row.AmountDeposited;
                    worksheet.Cell(currentRow, 5).Style.NumberFormat.Format = "₱#,##0.00";

                    // Salary Advance
                    worksheet.Cell(currentRow, 6).Value = row.SalaryAdvance ?? 0;
                    worksheet.Cell(currentRow, 6).Style.NumberFormat.Format = "₱#,##0.00";

                    // Commission
                    worksheet.Cell(currentRow, 7).Value = row.Commission;
                    worksheet.Cell(currentRow, 7).Style.NumberFormat.Format = "₱#,##0.00";

                    // Expenses
                    worksheet.Cell(currentRow, 8).Value = row.Expenses ?? 0;
                    worksheet.Cell(currentRow, 8).Style.NumberFormat.Format = "₱#,##0.00";

                    // Total Expenses
                    worksheet.Cell(currentRow, 9).Value = row.TotalExpenses;
                    worksheet.Cell(currentRow, 9).Style.NumberFormat.Format = "₱#,##0.00";

                    // Remarks
                    worksheet.Cell(currentRow, 10).Value = string.IsNullOrWhiteSpace(row.Remarks) ? "—" : row.Remarks;

                    // Gridlines at Borders
                    for (int col = 1; col <= headers.Length; col++)
                    {
                        worksheet.Cell(currentRow, col).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                        worksheet.Cell(currentRow, col).Style.Border.OutsideBorderColor = XLColor.FromHtml("#E0E0E0");
                    }

                    currentRow++;
                }

                worksheet.Columns().AdjustToContents();

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    return stream.ToArray();
                }
            }
        }

        private string SanitizeFileName(string fileName)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                fileName = fileName.Replace(c, '_');
            }
            return fileName;
        }
    }
}
