using ClosedXML.Excel;
using JustFlip.Class;
using JustFlip.DTO;
using JustFlip.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Org.BouncyCastle.Asn1.Ocsp;
using System.Drawing;
using System.IO.Compression;
using System.Security.Claims;

namespace JustFlip.Controllers
{
    [Authorize] // Protektado ng JWT, kailangan naka-login ang user
    [ApiController]
    [Route("api/report")]
    public class DailySalesDepositRecordController : ControllerBase
    {
        private readonly JustFlipDbContext _context;

        public DailySalesDepositRecordController(JustFlipDbContext context)
        {
            _context = context;
        }

        [HttpPost("save-report")]
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
        public async Task<IActionResult> GetRecords()
        {
            try
            {
                // 1. Kuhanin ang Role at BranchId mula sa JWT Token ng user
                var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
                var branchIdClaim = User.FindFirst("BranchId")?.Value;

                if (string.IsNullOrEmpty(branchIdClaim))
                {
                    return Unauthorized(new { message = "Branch configuration missing from token." });
                }

                int userBranchId = int.Parse(branchIdClaim);

                // 2. Base query: I-load ang records
                var query = _context.DailySalesDepositRecords.AsQueryable();

                // Security Filter: Kung hindi Admin, piliting ipakita lang ang sariling branch data
                if (userRole != "Admin")
                {
                    query = query.Where(r => r.BranchId == userBranchId);
                }

                var records = await query
                    .OrderByDescending(r => r.CreatedAt)
                    .Select(r => new DailySalesDepositRecordListDto
                    {
                        Id = r.Id,
                        ReportId = $"DS{r.Id:D5}",
                        ReportName = r.ReportName,
                        DateAndTimeCreated = r.CreatedAt.ToString("MMM dd, yyyy"),
                        TotalGrossCash = r.Rows.Sum(row => row.TotalGrossCash),
                        TotalCreditCardPayment = r.Rows.Sum(row => row.CreditCardPayment ?? 0),
                        TotalSalaryAdvance = r.Rows.Sum(row => row.SalaryAdvance ?? 0),
                        TotalCommission = r.Rows.Sum(row => row.Commission),
                        TotalExpenses = r.Rows.Sum(row => row.Expenses ?? 0),
                        TotalAmountExpenses = r.Rows.Sum(row => row.TotalExpenses),
                        TotalAmountDeposited = r.Rows.Sum(row => row.AmountDeposited)
                    })
                    .ToListAsync();

                return Ok(records);
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "An error occurred while fetching deposit records." });
            }
        }

        [HttpGet("{id:int}/details")]
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
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateRecord(int rowId, [FromBody] UpdateSaveSalesRecordRowDto request)
        {
            // 1. FluentValidation Process
            //var validator = new UpdateSaveSalesRecordRowDtoValidator();
            //var validatorResult = await validator.ValidateAsync(request);

            //if (!validatorResult.IsValid)
            //{
            //    var firstError = validatorResult.Errors.First();
            //    return BadRequest(new ErrorResponse(400, firstError.ErrorCode, firstError.ErrorMessage));
            //}

            // 2. Kuhanin ang User Claims (Role at BranchId galing sa JWT Token)
            var userRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
            var branchIdClaim = User.FindFirst("BranchId")?.Value;

            if (string.IsNullOrEmpty(branchIdClaim))
            {
                return Unauthorized(new ErrorResponse(401, "UNAUTHORIZED", "Branch assignment missing from authentication token."));
            }

            int userBranchId = int.Parse(branchIdClaim);
            var userBranchCode = await _context.Branches
                .Where(r => r.Id == userBranchId)
                .Select(b => b.BranchCode)
                .FirstOrDefaultAsync();

            // 3. Hanapin ang mismong Row gamit ang rowId KASAMA ang Parent Record para sa BranchId verification
            var row = await _context.DailySalesDepositRecordRows
                .Include(r => r.DailySalesDepositRecord) // 👈 Isinama ang Parent Record reference!
                .FirstOrDefaultAsync(r => r.Id == rowId);

            if (row == null)
            {
                return NotFound(new ErrorResponse(404, "NOT_FOUND", $"The sales deposit row with ID {rowId} was not found."));
            }

            // 4. 🔒 MULTI-BRANCH SECURITY CHECK
            // Chine-check kung ang BranchId ng Parent Report ay tumutugma sa BranchId ng naka-login na User
            if (userRole != "Admin" && row.BranchCode != userBranchCode)
            {
                return StatusCode(403, new ErrorResponse(403, "FORBIDDEN", "You are not authorized to modify records belonging to another branch."));
            }

            // 5. I-update ang mga properties ng row
            row.CashierName = request.CashierName;
            row.ControlNo = request.ControlNo;
            row.DateOfTransaction = request.DateOfTransaction;
            row.TotalGrossCash = request.TotalGrossCash;
            row.CreditCardPayment = request.CreditCardPayment;
            row.SalaryAdvance = request.SalaryAdvance;
            row.Commission = request.Commission;
            row.Expenses = request.Expenses;
            row.TotalExpenses = request.TotalExpenses;
            row.AmountDeposited = request.AmountDeposited;
            row.Remarks = request.Remarks;

            if (row.DailySalesDepositRecord != null)
            {
                row.DailySalesDepositRecord.LastDateUpdated = DateTime.UtcNow;
            }

            // 6. Save changes sa database
            await _context.SaveChangesAsync();

            return Ok(new SuccessfulResponse { message = $"Successfully updated row {rowId}!" });
        }

        [HttpPost("rows/delete/{rowId:int}")]
        public async Task<IActionResult> DeleteRecord(int id)
        {
            try
            {
                var userRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
                var branchIdClaim = User.FindFirst("BranchId")?.Value;

                if (string.IsNullOrEmpty(branchIdClaim))
                {
                    return Unauthorized(new { message = "Branch assignment missing from token." });
                }
                int userBranchId = int.Parse(branchIdClaim);
                var userBranchCode = await _context.Branches
                    .Where(r => r.Id == userBranchId)
                    .Select(b => b.BranchCode)
                    .FirstOrDefaultAsync();

                var record = await _context.DailySalesDepositRecordRows
                    .Include(r => r.DailySalesDepositRecord)
                    .FirstOrDefaultAsync(r => r.Id == id);

                if (record == null)
                {
                    return NotFound(new { message = "The report you are trying to delete does not exist." });
                }

                if (userRole != "Admin" && record.BranchCode != userBranchCode)
                {
                    return StatusCode(403, new ErrorResponse(403, "FORBIDDEN", "You are not authorized to modify records belonging to another branch."));
                }

                if (record.DailySalesDepositRecord != null)
                {
                    record.DailySalesDepositRecord.LastDateUpdated = DateTime.UtcNow;
                }

                _context.DailySalesDepositRecordRows.Remove(record);
                await _context.SaveChangesAsync();

                return Ok(new { message = "The entire report and its dynamic rows have been successfully deleted." });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "An internal error occurred while executing the delete sequence." });
            }
        }

        [HttpPost("{reportId:int}/rows/add")]
        [EnableRateLimiting("StrictWritePolicy")]
        [ProducesResponseType(typeof(AddSingleRowResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> AddSingleRowToReport(int reportId, [FromBody] AddSingleRowRequest request)
        {
            // 2. Kuhanin ang User Claims (Role at BranchCode/BranchId mula sa JWT Token)
            var userRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
            var branchIdClaim = User.FindFirst("BranchId")?.Value;

            if (string.IsNullOrEmpty(branchIdClaim))
            {
                return Unauthorized(new ErrorResponse(401, "UNAUTHORIZED", "Branch assignment missing from authentication token."));
            }

            int userBranchId = int.Parse(branchIdClaim);

            // 3. Hanapin ang Parent Report
            var parentReport = await _context.DailySalesDepositRecords.FindAsync(reportId);
            if (parentReport == null)
            {
                return NotFound(new ErrorResponse(404, "NOT_FOUND", $"The master report with ID {reportId} was not found."));
            }

            // 4. 🔒 MULTI-BRANCH SECURITY CHECK
            if (userRole != "Admin" && parentReport.BranchId != userBranchId)
            {
                return StatusCode(403, new ErrorResponse(403, "FORBIDDEN", "You are not authorized to add rows to another branch's report."));
            }

            var newRow = new DailySalesDepositRecordRow
            {
                BranchCode = parentReport.BranchCode,
                DailySalesDepositRecordId = reportId,
                CashierName = request.CashierName,
                ControlNo = request.ControlNo,
                DateOfTransaction = request.DateOfTransaction,
                TotalGrossCash = request.TotalGrossCash,
                CreditCardPayment = request.CreditCardPayment,
                SalaryAdvance = request.SalaryAdvance,
                Commission = request.Commission,
                Expenses = request.Expenses,
                TotalExpenses = request.TotalExpenses,
                AmountDeposited = request.AmountDeposited,
                Remarks = request.Remarks
            };

            _context.DailySalesDepositRecordRows.Add(newRow);
            await _context.SaveChangesAsync();

            return Ok(new AddSingleRowResponse { message = "Sucessfully added new report!", newRowId = newRow.Id });
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
