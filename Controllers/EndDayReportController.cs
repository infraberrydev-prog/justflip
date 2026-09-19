using JustFlip.DTO;
using JustFlip.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
        public async Task<IActionResult> SaveReport([FromBody] SaveEndDayReportDto dto)
        {
            try
            {
                var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
                var branchIdClaim = User.FindFirst("BranchId")?.Value;
                if (string.IsNullOrEmpty(branchIdClaim)) return Unauthorized();
                int userBranchId = int.Parse(branchIdClaim);

                EndDayReport? report;

                if (dto.Id > 0)
                {
                    // ==============================
                    // UPDATE & SYNC LOGIC
                    // ==============================
                    report = await _context.EndDayReports
                        .Include(r => r.Rows)
                        .FirstOrDefaultAsync(r => r.Id == dto.Id);

                    if (report == null) return NotFound(new { message = "Report not found." });
                    if (userRole != "Admin" && report.BranchId != userBranchId) return Forbid();

                    // Update common header fields
                    report.ReportName = dto.ReportName;
                    report.DateOfReport = dto.DateOfReport;
                    report.CashierName = dto.CashierName;

                    // Sync removals (Delete)
                    var incomingIds = dto.Rows.Where(r => r.Id > 0).Select(r => r.Id).ToList();
                    var toDelete = report.Rows.Where(r => !incomingIds.Contains(r.Id)).ToList();
                    foreach (var row in toDelete) _context.EndDayReportRows.Remove(row);

                    // Sync individual rows
                    foreach (var rowDto in dto.Rows)
                    {
                        if (rowDto.Id == 0)
                        {
                            report.Rows.Add(new EndDayReportRow
                            {
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
                    // ==============================
                    // CREATE LOGIC
                    // ==============================
                    report = new EndDayReport
                    {
                        BranchId = userBranchId,
                        ReportName = dto.ReportName,
                        DateOfReport = dto.DateOfReport,
                        CashierName = dto.CashierName,
                        CreatedAt = DateTime.UtcNow
                    };

                    foreach (var rowDto in dto.Rows)
                    {
                        report.Rows.Add(new EndDayReportRow
                        {
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
                return Ok(new { message = dto.Id > 0 ? "End day report updated!" : "End day report created!" });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Database transaction error." });
            }
        }
    }
}
