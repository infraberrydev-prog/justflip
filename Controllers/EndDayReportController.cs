using JustFlip.Class;
using JustFlip.DTO;
using JustFlip.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace JustFlip.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/end-day-report")]
    public class EndDayReportController : ControllerBase
    {
        private readonly JustFlipDbContext _context;
        private const string ReportsCacheTag = "reports-cache-tag";
        public EndDayReportController(JustFlipDbContext context)
        {
            _context = context;
        }

        [HttpPost("save")]
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

        // 1. UPDATE SINGLE ROW & TOUCH PARENT TIMESTAMP
        [HttpPost("rows/update/{rowId:int}")]
        [EnableRateLimiting("StrictWritePolicy")]
        [ProducesResponseType(typeof(SuccessfulResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateSingleRow(int rowId, [FromBody] UpdateEndDayReportRowDto request)
        {
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
            var branchIdClaim = User.FindFirst("BranchId")?.Value;

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

            await _context.SaveChangesAsync();

            return Ok(new SuccessfulResponse { message = $"Successfully updated row {rowId}!" });
        }

        // 2. ADD SINGLE ROW TO REPORT
        [HttpPost("{reportId:int}/rows/add")]
        [EnableRateLimiting("StrictWritePolicy")]
        [ProducesResponseType(typeof(AddSingleRowResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> AddSingleRowToReport(int reportId, [FromBody] AddEndDayRowRequest request)
        {
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
            var branchIdClaim = User.FindFirst("BranchId")?.Value;

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
            await _context.SaveChangesAsync();

            return Ok(new AddSingleRowResponse
            {
                message = "Successfully added new row to the report!",
                newRowId = newRow.Id
            });
        }

        // 3. DELETE SINGLE ROW
        [HttpDelete("rows/{rowId:int}")]
        [EnableRateLimiting("StrictWritePolicy")]
        [ProducesResponseType(typeof(SuccessfulResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteRow(int rowId)
        {
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
            var branchIdClaim = User.FindFirst("BranchId")?.Value;

            if (string.IsNullOrEmpty(branchIdClaim))
            {
                return Unauthorized(new ErrorResponse(401, "UNAUTHORIZED", "Branch assignment missing from token."));
            }

            int userBranchId = int.Parse(branchIdClaim);

            var row = await _context.EndDayReportRows
                .Include(r => r.EndDayReport)
                .FirstOrDefaultAsync(r => r.Id == rowId);

            if (row == null)
            {
                return NotFound(new ErrorResponse(404, "NOT_FOUND", $"Row with ID {rowId} was not found."));
            }

            if (userRole != "Admin" && row.EndDayReport?.BranchId != userBranchId)
            {
                return StatusCode(403, new ErrorResponse(403, "FORBIDDEN", "You are not authorized to delete rows from another branch's report."));
            }

            if (row.EndDayReport != null)
            {
                row.EndDayReport.LastDateUpdated = DateTime.UtcNow;
            }

            _context.EndDayReportRows.Remove(row);
            await _context.SaveChangesAsync();

            return Ok(new SuccessfulResponse { message = $"Successfully deleted row {rowId}!" });
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
    }
}
