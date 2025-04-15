using Microsoft.Reporting.WinForms;
using System;
using System.Collections.Generic;
using System.IO; // Для Path
using System.Windows;
using System.Windows.Controls;
using System.Linq;
using System.Data.Entity; // Для EF

namespace Buses_Try_14 // Убедитесь, что пространство имен ваше
{
    // Убедитесь, что этот класс определен (если он не в отдельном файле)
/*    public class BusScheduleReportItem
    {
        public int BusNumber { get; set; }
        public string BusType { get; set; }
        public int BusCapacity { get; set; }
        public string RouteDescription { get; set; }
        public DateTime DepartureDateTime { get; set; }
    }
*/
    public partial class ReportPage : Page
    {
        // Используем EF контекст для получения данных
        // Можно использовать ваш статический GetContext() или new BusStationEntities()
        // private BusStationEntities _context = BusStationEntities.GetContext(); // Пример

        public ReportPage()
        {
            InitializeComponent();
            // НЕ вызываем LoadReport здесь, так как ReportViewer еще может быть не готов в хосте
        }

        // Используем событие Loaded страницы, чтобы ReportViewer был точно инициализирован
        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            LoadReport();
        }

        // Метод получения данных (вернули его)
        // В ReportPage.xaml.cs

        public List<BusScheduleReportItem> GetBusScheduleReportData()
        {
            using (var context = new BusStationEntities())
            {
                try
                {
                    var query = context.Schedules
                                    .Include(s => s.Buses)
                                    .Include(s => s.Routes)
                                    .Where(s => s.Buses != null && s.Routes != null);

                    DateTime today = DateTime.Today;
                    query = query.Where(s => DbFunctions.TruncateTime(s.DepartureData) >= today);

                    // Шаг 1: Выбираем НЕОБХОДИМЫЕ ПОЛЯ из БД, включая дату и время РАЗДЕЛЬНО
                    var dataFromDb = query
                        .OrderBy(s => s.Buses.Number)
                        .ThenBy(s => s.DepartureData)
                        .ThenBy(s => s.DepartureTime)
                        .Select(s => new // Можно использовать анонимный тип для промежуточного результата
                        {
                            BusNumber = s.Buses.Number,
                            BusType = s.Buses.Type,
                            BusCapacity = s.Buses.CountOfSeats,
                            DepartuePoint = s.Routes.DepartuePoint,
                            Destination = s.Routes.Destination,
                            DepartureDate = s.DepartureData, // Выбираем дату как есть
                            DepartureTime = s.DepartureTime  // Выбираем время как есть
                        })
                        .ToList(); // <-- ВЫПОЛНЯЕМ ЗАПРОС К БАЗЕ ДАННЫХ ЗДЕСЬ

                    // Шаг 2: Преобразуем данные из БД в формат отчета, ВЫПОЛНЯЯ .Add() В ПАМЯТИ
                    var reportData = dataFromDb
                        .Select(s => new BusScheduleReportItem
                        {
                            BusNumber = s.BusNumber,
                            BusType = s.BusType,
                            BusCapacity = s.BusCapacity,
                            RouteDescription = (s.DepartuePoint ?? "?") + " - " + (s.Destination ?? "?"),
                            // Вот здесь .Add() теперь выполняется для объектов в памяти
                            DepartureDateTime = s.DepartureDate.Add(s.DepartureTime)
                        })
                        .ToList(); // Получаем итоговый список для отчета

                    return reportData;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка получения данных для отчета: {ex}");
                    MessageBox.Show($"Ошибка получения данных для отчета: {ex.Message}", "Ошибка данных", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return new List<BusScheduleReportItem>();
                }
            }
        }


        private void LoadReport()
        {
            // Получаем ReportViewer из WindowsFormsHost
            var reportViewer = this.ReportViewer; // Имя ReportViewer из XAML
            if (reportViewer == null)
            {
                MessageBox.Show("ReportViewer не найден в WindowsFormsHost.", "Ошибка инициализации", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                reportViewer.ProcessingMode = ProcessingMode.Local;
                reportViewer.LocalReport.DataSources.Clear(); // Очищаем старые источники

                // --- Используем ReportPath ---
                // Предполагаем, что файл отчета копируется в папку Reports в выходном каталоге
                // Если файл лежит рядом с .exe, уберите "Reports\\"
                string reportSubFolder = "Reports"; // Имя подпапки, если есть
                string reportFileName = "BusScheduleReport.rdlc"; // Имя вашего файла
                //string reportPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, reportSubFolder, reportFileName);
                string reportPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, reportFileName);

                // Если папки Reports нет, и файл лежит рядом с exe:
                // string reportPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, reportFileName);

                if (!File.Exists(reportPath))
                {
                    MessageBox.Show($"Файл отчета не найден по пути: {reportPath}\nУбедитесь, что свойство 'Копировать в выходной каталог' для RDLC файла установлено.",
                                    "Ошибка файла отчета", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                reportViewer.LocalReport.ReportPath = reportPath;
                // --- Конец ReportPath ---

                // --- Загружаем данные и добавляем ReportDataSource ---
                var reportData = GetBusScheduleReportData(); // Получаем данные из EF
                // Имя "BusScheduleData" должно совпадать с именем DataSet в RDLC, который основан на BusScheduleReportItem
                ReportDataSource rds = new ReportDataSource("DataSet1", reportData);
                reportViewer.LocalReport.DataSources.Add(rds);
                // --- Конец ReportDataSource ---

                reportViewer.RefreshReport();

                // --- Экспорт в Excel (как в вашем примере) ---
                // Вы можете вызывать это по кнопке или сразу после загрузки
                ExportReportToExcel(reportViewer);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке или экспорте отчета: {ex.ToString()}", "Ошибка отчета", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Метод экспорта (из вашего примера)
        private void ExportReportToExcel(ReportViewer reportViewer)
        {
            try
            {
                // Указываем формат Excel (для старых версий .xls)
                // Для новых .xlsx используйте "EXCELOPENXML"
                string format = "Excel"; // Или "EXCELOPENXML" для .xlsx
                string extension = ".xls"; // или ".xlsx"

                Warning[] warnings;
                string[] streamids;
                string mimeType;
                string encoding;
                string filenameExtension;

                byte[] bytes = reportViewer.LocalReport.Render(
                   format,
                   null, // deviceInfo
                   out mimeType,
                   out encoding,
                   out filenameExtension,
                   out streamids,
                   out warnings);

                // Сохраняем на рабочий стол
                string fileName = $"BusScheduleReport_{DateTime.Now:yyyyMMddHHmmss}{extension}"; // Добавляем дату к имени файла
                string filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), fileName);
                File.WriteAllBytes(filePath, bytes);

                MessageBox.Show($"Отчет сохранен на рабочем столе как:\n{filePath}", "Экспорт завершен", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при экспорте отчета в Excel: {ex.ToString()}", "Ошибка экспорта", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}