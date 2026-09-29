using JustFlip.Class;
using JustFlip.DTO;
using JustFlip.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
    }
}
