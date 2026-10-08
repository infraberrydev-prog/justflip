using ClosedXML.Excel;
using JustFlip.Class;
using JustFlip.DTO;
using JustFlip.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.IO.Compression;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace JustFlip.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/end-day-report")]
    public class EndDayReportController : ControllerBase
    {
        private readonly JustFlipDbContext _context;
        private readonly IOutputCacheStore _cacheStore;
        private const string ReportsCacheTag = "reports-cache-tag";
        public EndDayReportController(JustFlipDbContext context, IOutputCacheStore cacheStore)
        {
            _context = context;
            _cacheStore = cacheStore;
        }
        private async Task EvictReportsCacheAsync(CancellationToken cancellationToken = default)
        {
            await _cacheStore.EvictByTagAsync(ReportsCacheTag, cancellationToken);
        }

        [HttpPost("save-report")]
        [EnableRateLimiting("StrictWritePolicy")]
        [ProducesResponseType(typeof(SuccessfulResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> SaveReport([FromBody] SaveEndDayReportDto dto)
        {
            try
            {
                // 1. FluentValidation Process
                //var validator = new SaveEndDayReportDtoValidator();
                //var validatorResult = await validator.ValidateAsync(dto);

                //if (!validatorResult.IsValid)
                //{
                //    var firstError = validatorResult.Errors.First();
                //    return BadRequest(new ErrorResponse(400, firstError.ErrorCode, firstError.ErrorMessage));
                //}

                // 2. Kuhanin ang User Claims mula sa JWT Token
                var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
                var branchIdClaim = User.FindFirst("BranchId")?.Value;

                if (string.IsNullOrEmpty(branchIdClaim))
                {
                    return Unauthorized(new ErrorResponse(401, "UNAUTHORIZED", "Branch assignment missing from token."));
                }

                int userBranchId = int.Parse(branchIdClaim);

                var userBranchCode = await _context.Branches
                    .Where(b => b.Id == userBranchId)
                    .Select(b => b.BranchCode)
                    .FirstOrDefaultAsync();

                if (string.IsNullOrEmpty(userBranchCode))
                {
                    return BadRequest(new ErrorResponse(400, "INVALID_BRANCH", "Associated branch code not found."));
                }

                EndDayReport? report;

                if (dto.Id > 0)
                {
                    // ==========================================
                    // UPDATE & SYNC LOGIC
                    // ==========================================
                    report = await _context.EndDayReports
                        .Include(r => r.Rows)
                        .FirstOrDefaultAsync(r => r.Id == dto.Id);

                    if (report == null)
                    {
                        return NotFound(new ErrorResponse(404, "NOT_FOUND", $"End day report with ID {dto.Id} was not found."));
                    }

                    // 🔒 Multi-Branch Security Check
                    if (userRole != "Admin" && report.BranchId != userBranchId)
                    {
                        return StatusCode(403, new ErrorResponse(403, "FORBIDDEN", "You are not authorized to modify reports from another branch."));
                    }

                    // Update Header Properties
                    report.ReportName = dto.ReportName;
                    report.DateOfReport = dto.DateOfReport;
                    report.CashierName = dto.CashierName;

                    // 🎯 Update Parent Timestamp
                    report.LastDateUpdated = DateTime.UtcNow;

                    // Sync Removals (Delete removed rows)
                    var incomingIds = dto.Rows.Where(r => r.Id > 0).Select(r => r.Id).ToList();
                    var toDelete = report.Rows.Where(r => !incomingIds.Contains(r.Id)).ToList();

                    foreach (var row in toDelete)
                    {
                        _context.EndDayReportRows.Remove(row);
                    }

                    // Sync Rows (Add or Update)
                    foreach (var rowDto in dto.Rows)
                    {
                        if (rowDto.Id == 0)
                        {
                            report.Rows.Add(new EndDayReportRow
                            {
                                BranchCode = userBranchCode,
                                EmployeeName = rowDto.EmployeeName,
                                GrossAmount = rowDto.GrossAmount,
                                CreditCardPayment = rowDto.CreditCardPayment,
                                Commission = rowDto.Commission,
                                SalonExpenses = rowDto.SalonExpenses,
                                SalaryAdvance = rowDto.SalaryAdvance
                            });
                        }
                        else
                        {
                            var existingRow = report.Rows.FirstOrDefault(r => r.Id == rowDto.Id);
                            if (existingRow != null)
                            {
                                existingRow.EmployeeName = rowDto.EmployeeName;
                                existingRow.GrossAmount = rowDto.GrossAmount;
                                existingRow.CreditCardPayment = rowDto.CreditCardPayment;
                                existingRow.Commission = rowDto.Commission;
                                existingRow.SalonExpenses = rowDto.SalonExpenses;
                                existingRow.SalaryAdvance = rowDto.SalaryAdvance;
                            }
                        }
                    }
                }
                else
                {
                    // ==========================================
                    // CREATE LOGIC
                    // ==========================================
                    report = new EndDayReport
                    {
                        BranchId = userBranchId,
                        BranchCode = userBranchCode,
                        ReportName = dto.ReportName,
                        DateOfReport = dto.DateOfReport,
                        CashierName = dto.CashierName,
                        CreatedAt = DateTime.UtcNow,
                        LastDateUpdated = DateTime.UtcNow
                    };

                    foreach (var rowDto in dto.Rows)
                    {
                        report.Rows.Add(new EndDayReportRow
                        {
                            BranchCode = userBranchCode,
                            EmployeeName = rowDto.EmployeeName,
                            GrossAmount = rowDto.GrossAmount,
                            CreditCardPayment = rowDto.CreditCardPayment,
                            Commission = rowDto.Commission,
                            SalonExpenses = rowDto.SalonExpenses,
                            SalaryAdvance = rowDto.SalaryAdvance
                        });
                    }

                    _context.EndDayReports.Add(report);
                }

                await _context.SaveChangesAsync();

                return Ok(new SuccessfulResponse
                {
                    message = dto.Id > 0
                        ? $"Successfully updated end day report {report.Id}!"
                        : "Successfully created new end day report!"
                });
            }
            catch (Exception)
            {
                return StatusCode(500, new ErrorResponse(500, "INTERNAL_SERVER_ERROR", "An error occurred while saving the end day report."));
            }
        }

        // 1. UPDATE SINGLE ROW & TOUCH PARENT TIMESTAMP + AUDIT LOG
        [HttpPost("rows/update/{rowId:int}")]
        [EnableRateLimiting("StrictWritePolicy")]
        [ProducesResponseType(typeof(SuccessfulResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateSingleRow(int rowId, [FromBody] UpdateEndDayReportRowDto request)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
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

                // 📸 1. Capture Old Values before updating
                var oldValuesSnapshot = new
                {
                    row.EmployeeName,
                    row.GrossAmount,
                    row.CreditCardPayment,
                    row.Commission,
                    row.SalonExpenses,
                    row.SalaryAdvance
                };

                // 2. Apply Updates
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

                // 📸 3. Capture New Values
                var newValuesSnapshot = new
                {
                    request.EmployeeName,
                    request.GrossAmount,
                    request.CreditCardPayment,
                    request.Commission,
                    request.SalonExpenses,
                    request.SalaryAdvance
                };

                // 🛡️️ 4. Create AuditLog Record
                var auditLog = new AuditLogs
                {
                    EntityName = nameof(EndDayReportRow),
                    EntityId = row.Id,
                    Action = "UPDATE_ROW",
                    ModifiedBy = modifiedBy,
                    Timestamp = DateTime.UtcNow,
                    OldValues = JsonSerializer.Serialize(oldValuesSnapshot),
                    NewValues = JsonSerializer.Serialize(newValuesSnapshot),
                    ChangedColumns = "EmployeeName, GrossAmount, CreditCardPayment, Commission, SalonExpenses, SalaryAdvance"
                };

                _context.AuditLogs.Add(auditLog);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new SuccessfulResponse { message = $"Successfully updated row {rowId}!" });
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new ErrorResponse(500, "INTERNAL_SERVER_ERROR", "An internal error occurred while updating the report row."));
            }
        }


        // 2. ADD SINGLE ROW TO REPORT + AUDIT LOG
        [HttpPost("{reportId:int}/rows/add")]
        [EnableRateLimiting("StrictWritePolicy")]
        [ProducesResponseType(typeof(AddSingleRowResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> AddSingleRowToReport(int reportId, [FromBody] AddEndDayRowRequest request)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
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

                // 1. Instantiate New Row
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
                await _context.SaveChangesAsync(); // Para ma-generate ang newRow.Id

                // 🛡️ 2. Create AuditLog Record for ADD_ROW
                var auditLog = new AuditLogs
                {
                    EntityName = nameof(EndDayReportRow),
                    EntityId = newRow.Id,
                    Action = "ADD_ROW",
                    ModifiedBy = modifiedBy,
                    Timestamp = DateTime.UtcNow,
                    OldValues = null, // Walang old values dahil bagong nilikha
                    NewValues = JsonSerializer.Serialize(newRow),
                    ChangedColumns = "All Columns"
                };

                _context.AuditLogs.Add(auditLog);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                return Ok(new AddSingleRowResponse
                {
                    message = "Successfully added new row to the report!",
                    newRowId = newRow.Id
                });
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new ErrorResponse(500, "INTERNAL_SERVER_ERROR", "An internal error occurred while adding the row to the report."));
            }
        }

        // 3. DELETE SINGLE ROW
        [HttpPost("rows/delete/{rowId:int}")]
        [EnableRateLimiting("StrictWritePolicy")]
        [ProducesResponseType(typeof(SuccessfulResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> SoftDeleteRow(int rowId, [FromQuery] string? reason)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
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

                // 1. Query the active row record
                var row = await _context.EndDayReportRows
                    .Include(r => r.EndDayReport)
                    .FirstOrDefaultAsync(r => r.Id == rowId);

                if (row == null)
                {
                    return NotFound(new ErrorResponse(404, "NOT_FOUND", $"End day report row with ID {rowId} was not found in active records."));
                }

                // 2. Authorization Check
                if (userRole != "Admin" && row.EndDayReport?.BranchId != userBranchId)
                {
                    return StatusCode(403, new ErrorResponse(403, "FORBIDDEN", "You are not authorized to delete records belonging to another branch."));
                }

                // 3. Map to Archive Replica Entity
                var archivedRow = new DeletedEndDayReportRow
                {
                    OriginalRowId = row.Id,
                    EndDayReportId = row.EndDayReportId,
                    BranchCode = row.BranchCode,
                    EmployeeName = row.EmployeeName,
                    GrossAmount = row.GrossAmount,
                    CreditCardPayment = row.CreditCardPayment,
                    Commission = row.Commission,
                    SalonExpenses = row.SalonExpenses,
                    SalaryAdvance = row.SalaryAdvance,
                    DeletedAt = DateTime.UtcNow,
                    DeletedBy = modifiedBy,
                    DeletionReason = reason ?? "User soft-deleted row"
                };

                // 4. Touch Parent Update Timestamp
                if (row.EndDayReport != null)
                {
                    row.EndDayReport.LastDateUpdated = DateTime.UtcNow;
                }

                // 5. Create Audit Log
                var auditLog = new AuditLogs
                {
                    EntityName = nameof(EndDayReportRow),
                    EntityId = row.Id,
                    Action = "SOFT_DELETE_ARCHIVE_ROW",
                    ModifiedBy = modifiedBy,
                    Timestamp = DateTime.UtcNow,
                    OldValues = JsonSerializer.Serialize(row),
                    NewValues = JsonSerializer.Serialize(archivedRow),
                    ChangedColumns = "Moved to DeletedEndDayReportRows"
                };

                // 6. DB Execution
                _context.DeletedEndDayReportRows.Add(archivedRow);
                _context.EndDayReportRows.Remove(row);
                _context.AuditLogs.Add(auditLog);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new SuccessfulResponse { message = $"Row {rowId} successfully soft deleted and archived." });
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new ErrorResponse(500, "INTERNAL_SERVER_ERROR", "An internal error occurred while executing the row deletion sequence."));
            }
        }

        [HttpPost("delete/{id:int}")]
        [EnableRateLimiting("StrictWritePolicy")]
        [ProducesResponseType(typeof(SuccessfulResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> SoftDeleteMasterReport(int id, [FromQuery] string? reason)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
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

                // 1. Fetch parent report kasama ang lahat ng active child rows
                var report = await _context.EndDayReports
                    .Include(r => r.Rows)
                    .FirstOrDefaultAsync(r => r.Id == id);

                if (report == null)
                {
                    return NotFound(new ErrorResponse(404, "NOT_FOUND", $"End day report with ID {id} was not found in active records."));
                }

                // 2. Authorization Check
                if (userRole != "Admin" && report.BranchId != userBranchId)
                {
                    return StatusCode(403, new ErrorResponse(403, "FORBIDDEN", "You are not authorized to delete reports belonging to another branch."));
                }

                // 🛡️ JSON Serializer Options para i-ignore ang circular navigation properties
                var jsonOptions = new JsonSerializerOptions
                {
                    ReferenceHandler = ReferenceHandler.IgnoreCycles
                };

                // 3. Move all child rows to DeletedEndDayReportRows
                foreach (var childRow in report.Rows)
                {
                    var archivedChild = new DeletedEndDayReportRow
                    {
                        OriginalRowId = childRow.Id,
                        EndDayReportId = childRow.EndDayReportId,
                        BranchCode = childRow.BranchCode,
                        EmployeeName = childRow.EmployeeName,
                        GrossAmount = childRow.GrossAmount,
                        CreditCardPayment = childRow.CreditCardPayment,
                        Commission = childRow.Commission,
                        SalonExpenses = childRow.SalonExpenses,
                        SalaryAdvance = childRow.SalaryAdvance,
                        DeletedAt = DateTime.UtcNow,
                        DeletedBy = modifiedBy,
                        DeletionReason = reason ?? "Parent master report deleted"
                    };

                    _context.DeletedEndDayReportRows.Add(archivedChild);
                }

                // 4. Map parent values to Replica Model
                var archivedReport = new DeletedEndDayReport
                {
                    OriginalReportId = report.Id,
                    BranchId = report.BranchId,
                    BranchCode = report.BranchCode,
                    ReportName = report.ReportName,
                    DateOfReport = report.DateOfReport,
                    CashierName = report.CashierName,
                    OriginalCreatedAt = report.CreatedAt,
                    LastDateUpdated = report.LastDateUpdated,
                    DeletedAt = DateTime.UtcNow,
                    DeletedBy = modifiedBy,
                    DeletionReason = reason ?? "User soft-deleted master report",
                    ArchivedRowsJson = JsonSerializer.Serialize(report.Rows, jsonOptions)
                };

                // 5. Create Audit Log
                var auditLog = new AuditLogs
                {
                    EntityName = nameof(EndDayReport),
                    EntityId = report.Id,
                    Action = "SOFT_DELETE_ARCHIVE_MASTER_REPORT",
                    ModifiedBy = modifiedBy,
                    Timestamp = DateTime.UtcNow,
                    OldValues = JsonSerializer.Serialize(report, jsonOptions),
                    NewValues = JsonSerializer.Serialize(archivedReport, jsonOptions),
                    ChangedColumns = "Moved parent report to DeletedEndDayReports and child rows to DeletedEndDayReportRows"
                };

                // 6. DB Execution: Remove active records & add archived replicas
                _context.DeletedEndDayReports.Add(archivedReport);
                _context.EndDayReportRows.RemoveRange(report.Rows);
                _context.EndDayReports.Remove(report);
                _context.AuditLogs.Add(auditLog);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new SuccessfulResponse { message = $"Master report {id} and its rows were successfully soft-deleted and archived." });
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new ErrorResponse(500, "INTERNAL_SERVER_ERROR", "An error occurred while executing the master report deletion sequence."));
            }
        }

        [HttpGet("get-all-reports")]
        [OutputCache(PolicyName = "ReportsListCache", Tags = [ReportsCacheTag])]
        [ProducesResponseType(typeof(PagedEndDayReportResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetAllEndDayReports(
            [FromQuery] string? search,
            [FromQuery] DateOnly? startDateCreated,
            [FromQuery] DateOnly? endDateCreated,
            [FromQuery] decimal? minRevenue,
            [FromQuery] decimal? maxRevenue,
            [FromQuery] string? lastUpdated = "ALL", // Short codes: TOD, L7D, L30D, TM, ALL
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? sortBy = "createdAt", // Default Column
            [FromQuery] string? sortOrder = "DESC")   // ASC, DESC
        {
            try
            {
                // 1. JWT Security & Branch Extraction
                var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
                var branchIdClaim = User.FindFirst("BranchId")?.Value;

                if (string.IsNullOrEmpty(branchIdClaim))
                {
                    return Unauthorized(new ErrorResponse(401, "UNAUTHORIZED", "Branch configuration missing from token."));
                }

                int userBranchId = int.Parse(branchIdClaim);

                if (page < 1) page = 1;
                if (pageSize < 1) pageSize = 10;

                // 2. Base Query
                var query = _context.EndDayReports.AsNoTracking();

                // 🔒 Multi-Branch Security Filter
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
                {
                    query = query.Where(r => r.CreatedAt >= startDateCreated.Value.ToDateTime(TimeOnly.MinValue));
                }

                if (endDateCreated.HasValue)
                {
                    query = query.Where(r => r.CreatedAt <= endDateCreated.Value.ToDateTime(TimeOnly.MaxValue));
                }

                // 5. Revenue Filter (Total Gross Cash per Report)
                if (minRevenue.HasValue)
                {
                    query = query.Where(r => r.Rows.Sum(row => (decimal?)row.GrossAmount) >= minRevenue.Value);
                }

                if (maxRevenue.HasValue)
                {
                    query = query.Where(r => r.Rows.Sum(row => (decimal?)row.GrossAmount) <= maxRevenue.Value);
                }

                // 6. Last Updated Quick Filter
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

                // 7. Dynamic Sorting Matrix (Aligned sa Columns ng UI)
                bool isDesc = string.Equals(sortOrder, "DESC", StringComparison.OrdinalIgnoreCase);

                query = sortBy?.ToLower() switch
                {
                    "dateofreport" => isDesc
                        ? query.OrderByDescending(r => r.DateOfReport)
                        : query.OrderBy(r => r.DateOfReport),

                    "totalgross" or "gross" => isDesc
                        ? query.OrderByDescending(r => r.Rows.Sum(row => (decimal?)row.GrossAmount) ?? 0)
                        : query.OrderBy(r => r.Rows.Sum(row => (decimal?)row.GrossAmount) ?? 0),

                    "creditcard" => isDesc
                        ? query.OrderByDescending(r => r.Rows.Sum(row => (decimal?)row.CreditCardPayment) ?? 0)
                        : query.OrderBy(r => r.Rows.Sum(row => (decimal?)row.CreditCardPayment) ?? 0),

                    "commission" => isDesc
                        ? query.OrderByDescending(r => r.Rows.Sum(row => (decimal?)row.Commission) ?? 0)
                        : query.OrderBy(r => r.Rows.Sum(row => (decimal?)row.Commission) ?? 0),

                    "salonexpenses" or "expenses" => isDesc
                        ? query.OrderByDescending(r => r.Rows.Sum(row => (decimal?)row.SalonExpenses) ?? 0)
                        : query.OrderBy(r => r.Rows.Sum(row => (decimal?)row.SalonExpenses) ?? 0),

                    "salaryadvance" => isDesc
                        ? query.OrderByDescending(r => r.Rows.Sum(row => (decimal?)row.SalaryAdvance) ?? 0)
                        : query.OrderBy(r => r.Rows.Sum(row => (decimal?)row.SalaryAdvance) ?? 0),

                    "reportname" or "name" => isDesc
                        ? query.OrderByDescending(r => r.ReportName)
                        : query.OrderBy(r => r.ReportName),

                    "lastupdated" => isDesc
                        ? query.OrderByDescending(r => r.LastDateUpdated ?? r.CreatedAt)
                        : query.OrderBy(r => r.LastDateUpdated ?? r.CreatedAt),

                    _ => isDesc // Default: CreatedAt
                        ? query.OrderByDescending(r => r.CreatedAt)
                        : query.OrderBy(r => r.CreatedAt)
                };

                // 8. Projection and Pagination
                var records = await query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(r => new EndDayReportListDto(
                        r.Id,
                        $"ED{r.Id:D5}", // Output format: ED05234
                        r.ReportName,
                        r.DateOfReport.ToString("MMM dd, yyyy"),
                        r.Rows.Sum(row => row.GrossAmount),
                        r.Rows.Sum(row => row.CreditCardPayment ?? 0),
                        r.Rows.Sum(row => row.Commission),
                        r.Rows.Sum(row => row.SalonExpenses ?? 0),
                        r.Rows.Sum(row => row.SalaryAdvance ?? 0),
                        // Cash in Register Formula: Gross Cash - Credit Card - Expenses - Salary Advance
                        r.Rows.Sum(row => row.GrossAmount
                                          - (row.CreditCardPayment ?? 0)
                                          - (row.SalonExpenses ?? 0)
                                          - (row.SalaryAdvance ?? 0)),
                        r.CreatedAt.ToString("MMM d, yyyy - HH:mm:ss"),
                        r.LastDateUpdated.HasValue ? r.LastDateUpdated.Value.ToString("MMM d, yyyy - HH:mm:ss") : "—"
                    ))
                    .ToListAsync();

                int totalPages = (int)Math.Ceiling((double)totalRecords / pageSize);

                return Ok(new PagedEndDayReportResponse(records, totalRecords, page, totalPages == 0 ? 1 : totalPages, pageSize));
            }
            catch (Exception)
            {
                return StatusCode(500, new ErrorResponse(500, "INTERNAL_SERVER_ERROR", "An error occurred while retrieving end day reports."));
            }
        }

        [HttpGet("{id:int}/details")]
        [OutputCache(Duration = 300, Tags = [ReportsCacheTag])]
        [ProducesResponseType(typeof(EndDayReportDetailDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetEndDayReportDetailsById(int id)
        {
            try
            {
                // 1. Kuhanin ang Claims para sa Security Check
                var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
                var branchIdClaim = User.FindFirst("BranchId")?.Value;

                if (string.IsNullOrEmpty(branchIdClaim))
                {
                    return Unauthorized(new ErrorResponse(401, "UNAUTHORIZED", "Branch assignment missing from authentication token."));
                }

                int userBranchId = int.Parse(branchIdClaim);

                // 2. Fetch Parent Report Record kasama ang Rows
                var report = await _context.EndDayReports
                    .AsNoTracking()
                    .Include(r => r.Rows)
                    .FirstOrDefaultAsync(r => r.Id == id);

                if (report == null)
                {
                    return NotFound(new ErrorResponse(404, "NOT_FOUND", $"The requested end day report with ID {id} does not exist."));
                }

                // 3. 🔒 Security Check: Siguraduhin na hindi ma-pe-peek ng ibang branch ang data
                if (userRole != "Admin" && report.BranchId != userBranchId)
                {
                    return StatusCode(403, new ErrorResponse(403, "FORBIDDEN", "You are not authorized to view end day reports belonging to another branch."));
                }

                // 4. Transform at Mapped Details Response
                var mappedRows = report.Rows.Select(row => new EndDayReportRowDetailDto(
                    row.Id,
                    row.EmployeeName,
                    row.GrossAmount,
                    row.CreditCardPayment,
                    row.Commission,
                    row.SalonExpenses,
                    row.SalaryAdvance,
                    row.GrossAmount - (row.CreditCardPayment ?? 0) - (row.SalonExpenses ?? 0) - (row.SalaryAdvance ?? 0)
                )).ToList();

                var detailDto = new EndDayReportDetailDto
                {
                    Id = report.Id,
                    FormattedReportId = $"ED{report.Id:D5}",
                    ReportName = report.ReportName,
                    DateOfReport = report.DateOfReport.ToString("MMM dd, yyyy"),
                    CashierName = report.CashierName,
                    DateCreated = report.CreatedAt.ToString("MMM d, yyyy - HH:mm:ss"),
                    LastUpdated = report.LastDateUpdated.HasValue ? report.LastDateUpdated.Value.ToString("MMM d, yyyy - HH:mm:ss") : null,

                    // Grand Totals Computation
                    TotalGrossCash = mappedRows.Sum(r => r.GrossAmount),
                    TotalCreditCard = mappedRows.Sum(r => r.CreditCardPayment ?? 0),
                    TotalCommission = mappedRows.Sum(r => r.Commission),
                    TotalSalonExpenses = mappedRows.Sum(r => r.SalonExpenses ?? 0),
                    TotalSalaryAdvance = mappedRows.Sum(r => r.SalaryAdvance ?? 0),
                    TotalCashInRegister = mappedRows.Sum(r => r.CashInRegister),

                    Rows = mappedRows
                };

                return Ok(detailDto);
            }
            catch (Exception)
            {
                return StatusCode(500, new ErrorResponse(500, "INTERNAL_SERVER_ERROR", "An error occurred while retrieving end day report details."));
            }
        }

        // ==========================================
        // DOWNLOAD ENDPOINT (SINGLE EXCEL OR ZIP)
        // ==========================================
        [HttpPost("download")]
        [EnableRateLimiting("DownloadPolicy")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DownloadEndDayReports([FromBody] DownloadReportsRequest request)
        {
            if (request?.ReportIds == null || !request.ReportIds.Any())
            {
                return BadRequest(new ErrorResponse(400, "INVALID_REQUEST", "Please select at least one report to download."));
            }

            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
            var branchIdClaim = User.FindFirst("BranchId")?.Value;

            if (string.IsNullOrEmpty(branchIdClaim))
            {
                return Unauthorized(new ErrorResponse(401, "UNAUTHORIZED", "Branch assignment missing from token."));
            }

            int userBranchId = int.Parse(branchIdClaim);

            // 1. Fetch requested reports with their child rows
            var reports = await _context.EndDayReports
                .Include(r => r.Rows)
                .Where(r => request.ReportIds.Contains(r.Id))
                .ToListAsync();

            if (!reports.Any())
            {
                return NotFound(new ErrorResponse(404, "NOT_FOUND", "No matching end day reports found for the selected IDs."));
            }

            // 2. Branch Authorization Filter
            if (userRole != "Admin")
            {
                var unauthorizedReports = reports.Where(r => r.BranchId != userBranchId).ToList();
                if (unauthorizedReports.Any())
                {
                    return StatusCode(403, new ErrorResponse(403, "FORBIDDEN", "You are not authorized to download reports from another branch."));
                }
            }

            // ==========================================
            // CASE A: SINGLE REPORT SELECTED (.xlsx)
            // ==========================================
            if (reports.Count == 1)
            {
                var report = reports.First();
                var fileBytes = GenerateEndDayReportExcelBytes(report);
                string fileName = $"{SanitizeFileName(report.ReportName)}.xlsx";

                return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }

            // ==========================================
            // CASE B: MULTIPLE REPORTS SELECTED (.zip)
            // ==========================================
            using (var zipStream = new MemoryStream())
            {
                using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, true))
                {
                    foreach (var report in reports)
                    {
                        var fileBytes = GenerateEndDayReportExcelBytes(report);
                        var entryName = $"{SanitizeFileName(report.ReportName)}_{report.Id}.xlsx";

                        var entry = archive.CreateEntry(entryName, CompressionLevel.Fastest);
                        using (var entryStream = entry.Open())
                        {
                            await entryStream.WriteAsync(fileBytes, 0, fileBytes.Length);
                        }
                    }
                }

                zipStream.Position = 0;
                string zipFileName = $"EndDay_Reports_{DateTime.UtcNow:yyyyMMdd_HHmmss}.zip";

                return File(zipStream.ToArray(), "application/zip", zipFileName);
            }
        }

        // ==========================================
        // HELPER METHODS FOR EXCEL GENERATION
        // ==========================================
        private byte[] GenerateEndDayReportExcelBytes(EndDayReport report)
        {
            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("End Day Report");

                // 1. Report Title & Sub-header (Base sa UI Screenshot)
                worksheet.Cell(1, 1).Value = report.ReportName; // e.g., "Payment report for the month of March 2026"
                worksheet.Cell(1, 1).Style.Font.Bold = true;
                worksheet.Cell(1, 1).Style.Font.FontSize = 16;

                worksheet.Cell(2, 1).Value = report.CreatedAt.ToString("MMM d, yyyy - HH:mm:ss");
                worksheet.Cell(2, 1).Style.Font.FontColor = XLColor.Gray;

                // 2. Exact Column Headers mula sa UI
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
                    "Remarks",
                    "Total Amount"
                };

                int headerRowIndex = 4;
                for (int col = 0; col < headers.Length; col++)
                {
                    var cell = worksheet.Cell(headerRowIndex, col + 1);
                    cell.Value = headers[col];
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#F9FAFB");
                    cell.Style.Font.FontColor = XLColor.FromHtml("#4B5563");
                    cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    cell.Style.Border.OutsideBorderColor = XLColor.FromHtml("#E5E7EB");
                }

                // 3. Populate EndDayReportRow Data
                int currentRow = 5;
                foreach (var row in report.Rows)
                {
                    // Employee Name / Cashier
                    worksheet.Cell(currentRow, 1).Value = string.IsNullOrWhiteSpace(row.EmployeeName) ? report.CashierName : row.EmployeeName;

                    // Date of Transaction (gamit ang DateOfReport ng parent report)
                    worksheet.Cell(currentRow, 2).Value = report.DateOfReport.ToString("MMM d, yyyy");

                    // Total Gross (Cash) -> GrossAmount
                    worksheet.Cell(currentRow, 3).Value = row.GrossAmount;
                    worksheet.Cell(currentRow, 3).Style.NumberFormat.Format = "₱#,##0.00";

                    // Credit Card Payment
                    worksheet.Cell(currentRow, 4).Value = row.CreditCardPayment.HasValue && row.CreditCardPayment.Value > 0 ? row.CreditCardPayment.Value : "—";
                    if (row.CreditCardPayment.HasValue && row.CreditCardPayment.Value > 0)
                        worksheet.Cell(currentRow, 4).Style.NumberFormat.Format = "₱#,##0.00";

                    // Total Amount Deposited (Calculated cash deposited value)
                    decimal amountDeposited = row.GrossAmount - (row.CreditCardPayment ?? 0);
                    worksheet.Cell(currentRow, 5).Value = amountDeposited;
                    worksheet.Cell(currentRow, 5).Style.NumberFormat.Format = "₱#,##0.00";

                    // Salary Advance
                    worksheet.Cell(currentRow, 6).Value = row.SalaryAdvance.HasValue && row.SalaryAdvance.Value > 0 ? row.SalaryAdvance.Value : "—";
                    if (row.SalaryAdvance.HasValue && row.SalaryAdvance.Value > 0)
                        worksheet.Cell(currentRow, 6).Style.NumberFormat.Format = "₱#,##0.00";

                    // Commission
                    worksheet.Cell(currentRow, 7).Value = row.Commission > 0 ? row.Commission : "—";
                    if (row.Commission > 0)
                        worksheet.Cell(currentRow, 7).Style.NumberFormat.Format = "₱#,##0.00";

                    // Expenses / SalonExpenses
                    worksheet.Cell(currentRow, 8).Value = row.SalonExpenses.HasValue && row.SalonExpenses.Value > 0 ? row.SalonExpenses.Value : "—";
                    if (row.SalonExpenses.HasValue && row.SalonExpenses.Value > 0)
                        worksheet.Cell(currentRow, 8).Style.NumberFormat.Format = "₱#,##0.00";

                    // Total Expenses
                    decimal totalExpenses = (row.SalonExpenses ?? 0) + (row.SalaryAdvance ?? 0);
                    worksheet.Cell(currentRow, 9).Value = totalExpenses > 0 ? totalExpenses : "—";
                    if (totalExpenses > 0)
                        worksheet.Cell(currentRow, 9).Style.NumberFormat.Format = "₱#,##0.00";

                    // Remarks
                    worksheet.Cell(currentRow, 10).Value = "—";

                    // Total Amount (Net Take)
                    decimal totalAmount = row.GrossAmount
                                        - (row.CreditCardPayment ?? 0)
                                        - row.Commission
                                        - (row.SalonExpenses ?? 0)
                                        - (row.SalaryAdvance ?? 0);

                    worksheet.Cell(currentRow, 11).Value = totalAmount;
                    worksheet.Cell(currentRow, 11).Style.NumberFormat.Format = "₱#,##0.00";

                    // Row Borders Styling
                    for (int col = 1; col <= headers.Length; col++)
                    {
                        worksheet.Cell(currentRow, col).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                        worksheet.Cell(currentRow, col).Style.Border.OutsideBorderColor = XLColor.FromHtml("#F3F4F6");
                    }

                    currentRow++;
                }

                // 4. Auto-fit columns to content length
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
