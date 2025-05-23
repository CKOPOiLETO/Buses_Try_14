﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Data.Entity; // <--- ДОБАВЬТЕ ЭТУ СТРОКУ
using System.Runtime.InteropServices; // Для Marshal.ReleaseComObject
using Excel = Microsoft.Office.Interop.Excel; // Псевдоним для удобства
using Microsoft.Win32;
using System.Reflection; // Для Type.Missing
using Word = Microsoft.Office.Interop.Word; // Псевдоним для Word


namespace Buses_Try_14
{
    /// <summary>
    /// Логика взаимодействия для SchedulePage.xaml
    /// </summary>
    public partial class SchedulePage : Page
    {
        public SchedulePage()
        {
            InitializeComponent();
            // _context = BusStationEntities.GetContext(); // Не храним долгоживущий контекст
        }

        // Метод для загрузки и отображения данных
        // Обновленный метод для загрузки данных с учетом фильтров
        private void LoadScheduleData(DateTime? dateFilter = null)
        {
            // Получаем выбранные значения из RouteSelector по его имени (x:Name)
            // Используем ?. на случай, если контрол еще не инициализирован
            string selectedDeparture = FilterRouteSelector?.SelectedDeparturePoint;
            string selectedDestination = FilterRouteSelector?.SelectedDestinationPoint;

            using (var context = new BusStationEntities()) // Используем ваш контекст
            {
                try
                {
                    IQueryable<Schedules> query = context.Schedules
                                                         .Include(s => s.Routes)
                                                         .Include(s => s.Buses);

                    // --- ОБНОВЛЕННАЯ ФИЛЬТРАЦИЯ ПО МАРШРУТУ ---
                    // Применяем фильтр, если пункт отправления выбран в RouteSelector
                    if (!string.IsNullOrWhiteSpace(selectedDeparture))
                    {
                        // Сравнение на точное совпадение
                        query = query.Where(s => s.Routes.DepartuePoint == selectedDeparture);
                    }

                    // Применяем фильтр, если пункт назначения выбран в RouteSelector
                    if (!string.IsNullOrWhiteSpace(selectedDestination))
                    {
                        // Сравнение на точное совпадение
                        query = query.Where(s => s.Routes.Destination == selectedDestination);
                    }
                    // --- КОНЕЦ ОБНОВЛЕНИЯ ФИЛЬТРАЦИИ МАРШРУТА ---

                    // Фильтр по дате остается как был
                    if (dateFilter.HasValue)
                    {
                        DateTime dateToFilter = dateFilter.Value.Date;
                        query = query.Where(s => DbFunctions.TruncateTime(s.DepartureData) == dateToFilter);
                    }

                    var scheduleList = query.OrderBy(s => s.DepartureData)
                                            .ThenBy(s => s.DepartureTime)
                                            .ToList();

                    DGridSchedules.ItemsSource = scheduleList;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка загрузки данных: {ex.Message}\n\nВнутренняя ошибка:\n{ex.InnerException?.Message}",
                                    "Ошибка базы данных", MessageBoxButton.OK, MessageBoxImage.Error);
                    DGridSchedules.ItemsSource = null;
                }
            }
        }

        // Обновляем данные при появлении страницы на экране
        private void Page_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (Visibility == Visibility.Visible && IsLoaded) // Проверяем IsLoaded, чтобы FilterRouteSelector был доступен
            {
                // Передаем только дату, значения маршрута возьмем из UserControl внутри метода
                LoadScheduleData(DpFilterDate.SelectedDate);
            }
        }

        // Кнопка "Добавить рейс"
        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            Manager.MainFrame.Navigate(new AddEditSchedulePage(null)); // Передаем null для создания нового рейса
        }

        // Кнопка "Редактировать" в строке DataGrid
        private void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            // Получаем объект Schedules из DataContext кнопки
            var selectedSchedule = (sender as Button)?.DataContext as Schedules;
            if (selectedSchedule != null)
            {
                Manager.MainFrame.Navigate(new AddEditSchedulePage(selectedSchedule));
            }
        }

        // Кнопка "Удалить рейс(ы)"
        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            // Получаем выделенные элементы (может быть несколько)
            var schedulesToDelete = DGridSchedules.SelectedItems.Cast<Schedules>().ToList();

            if (!schedulesToDelete.Any())
            {
                MessageBox.Show("Пожалуйста, выберите один или несколько рейсов для удаления.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Запрос подтверждения
            if (MessageBox.Show($"Вы точно хотите удалить выбранные {schedulesToDelete.Count} рейс(ов)?",
                               "Подтверждение удаления", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                try
                {
                    var currentContext = BusStationEntities.GetContext();
                    // Важно! Если связи настроены с каскадным удалением или есть зависимые записи (Tickets),
                    // может потребоваться дополнительная логика или обработка ошибок.
                    // Проверяем, есть ли билеты на удаляемые рейсы
                    foreach (var schedule in schedulesToDelete)
                    {
                        // Нужно подгрузить билеты для проверки, если они не загружены
                        // или проверить через запрос к БД
                        bool hasTickets = currentContext.Tickets.Any(t => t.ScheduleID == schedule.Id);
                        if (hasTickets)
                        {
                            MessageBox.Show($"Невозможно удалить рейс {schedule.Routes.DepartuePoint}-{schedule.Routes.Destination} от {schedule.DepartureData.ToShortDateString()}, так как на него проданы билеты.",
                                            "Ошибка удаления", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return; // Прерываем удаление, если хотя бы на один рейс есть билеты
                        }
                    }


                    // Удаляем объекты из контекста
                    currentContext.Schedules.RemoveRange(schedulesToDelete);
                    // Сохраняем изменения в БД
                    currentContext.SaveChanges();
                    MessageBox.Show("Выбранные рейсы успешно удалены.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);

                    // Обновляем DataGrid
                    LoadScheduleData();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при удалении рейсов: {ex.Message}\n\nВнутренняя ошибка:\n{ex.InnerException?.Message}",
                                    "Ошибка базы данных", MessageBoxButton.OK, MessageBoxImage.Error);
                    // Можно попытаться перезагрузить контекст, если ошибка связана с отслеживанием объектов
                }
            }
        }

        private void BtnGoToPassengers_Click(object sender, RoutedEventArgs e)
        {
            Manager.MainFrame.Navigate(new PassengersPage());
        }

        // В SchedulePage.xaml.cs

        // Обработчик для новой кнопки
        private void BtnSellTicket_Click(object sender, RoutedEventArgs e)
        {
            // Получаем выбранный рейс из DataGrid
            var selectedSchedule = DGridSchedules.SelectedItem as Schedules; // Убедитесь, что SelectionMode="Single" или Extended

            if (selectedSchedule == null)
            {
                MessageBox.Show("Пожалуйста, выберите рейс из списка для продажи билета.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // --- Проверка наличия мест ---
            int availableSeats = CalculateAvailableSeats(selectedSchedule.Id);
            if (availableSeats == -1) // Ошибка при расчете
            {
                return; // Сообщение об ошибке уже было показано в CalculateAvailableSeats
            }
            if (availableSeats <= 0)
            {
                MessageBox.Show("На выбранный рейс нет свободных мест.", "Нет мест", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Если места есть, переходим на страницу продажи, передав ID рейса
            Manager.MainFrame.Navigate(new SellTicketPage(selectedSchedule.Id));
        }

        // Вспомогательный метод для расчета свободных мест
        private int CalculateAvailableSeats(int scheduleId)
        {
            try // Обернем в try-catch на случай ошибок БД
            {
                using (var context = new BusStationEntities())
                {
                    // Получаем рейс ВМЕСТЕ с автобусом, чтобы знать общее кол-во мест
                    var schedule = context.Schedules
                                          .Include(s => s.Buses) // Важно подгрузить автобус
                                          .FirstOrDefault(s => s.Id == scheduleId);

                    if (schedule == null || schedule.Buses == null)
                    {
                        MessageBox.Show($"Не удалось получить информацию о рейсе (ID={scheduleId}) или связанном автобусе.", "Ошибка данных", MessageBoxButton.OK, MessageBoxImage.Error);
                        return -1; // Признак ошибки
                    }

                    int totalSeats = schedule.Buses.CountOfSeats;
                    // Считаем уже проданные билеты на этот рейс
                    int soldSeats = context.Tickets.Count(t => t.ScheduleID == scheduleId);
                    return totalSeats - soldSeats;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при расчете свободных мест: {ex.Message}", "Ошибка БД", MessageBoxButton.OK, MessageBoxImage.Error);
                return -1; // Признак ошибки
            }
        }

        private void BtnViewPassengers_Click(object sender, RoutedEventArgs e)
        {
            // Получаем выбранный рейс из DataGrid
            var selectedSchedule = DGridSchedules.SelectedItem as Schedules;

            if (selectedSchedule == null)
            {
                MessageBox.Show("Пожалуйста, выберите рейс из списка для просмотра пассажиров.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Переходим на новую страницу, передав ID выбранного рейса
            Manager.MainFrame.Navigate(new ViewPassengersOnSchedulePage(selectedSchedule.Id));
        }

        private void BtnApplyFilters_Click(object sender, RoutedEventArgs e)
        {
            // Передаем только дату
            LoadScheduleData(DpFilterDate.SelectedDate);
        }

        private void BtnResetFilters_Click(object sender, RoutedEventArgs e)
        {
            // Сбрасываем значения в UserControl и DatePicker
            if (FilterRouteSelector != null) // Проверяем на null
            {
                FilterRouteSelector.SelectedDeparturePoint = null;
                FilterRouteSelector.SelectedDestinationPoint = null;
                // Также может потребоваться сбросить текст, если ComboBox IsEditable=True
                FilterRouteSelector.ComboDeparturePoint.Text = string.Empty; // Пример, если ComboBox назван так внутри UserControl
                FilterRouteSelector.ComboDestinationPoint.Text = string.Empty; // Пример
            }

            DpFilterDate.SelectedDate = null;

            // Загружаем все данные без фильтров
            LoadScheduleData();
        }

        private void BtnGoToReport_Click(object sender, RoutedEventArgs e)
        {
            Manager.MainFrame.Navigate(new ReportPage());
        }

        private void BtnExportToExcel_Click(object sender, RoutedEventArgs e)
        {
          
            List<Schedules> allSchedules;
            try
            {
                using (var context = new BusStationEntities())
                {
                    allSchedules = context.Schedules
                                        .Include(s => s.Routes) 
                                        .Include(s => s.Buses)  
                                        .Where(s => s.Routes != null && s.Buses != null) 
                                        .OrderBy(s => s.Routes.DepartuePoint) 
                                        .ThenBy(s => s.Routes.Destination)
                                        .ThenBy(s => s.DepartureData)  
                                        .ThenBy(s => s.DepartureTime)   
                                        .ToList();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке данных для экспорта: {ex.Message}", "Ошибка данных", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }


            if (!allSchedules.Any())
            {
                MessageBox.Show("Нет данных для экспорта.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

        
            var groupedByRoute = allSchedules
                .GroupBy(s => new { s.RouteID, RouteName = $"{s.Routes.DepartuePoint} - {s.Routes.Destination}" })
                .OrderBy(g => g.Key.RouteName); 

       
            Excel.Application excelApp = null;
            Excel.Workbook workbook = null;

            try
            {
                excelApp = new Excel.Application();
                // excelApp.Visible = true; 
                excelApp.DisplayAlerts = false; 
                workbook = excelApp.Workbooks.Add(Type.Missing); 

                int sheetIndex = 0;

               
                foreach (var routeGroup in groupedByRoute)
                {
                    sheetIndex++;
                    Excel.Worksheet worksheet = null;

                    // Получаем или добавляем лист
                    if (sheetIndex == 1 && workbook.Sheets.Count >= 1)
                    {
                        worksheet = (Excel.Worksheet)workbook.Sheets[1]; 
                    }
                    else
                    {
                        worksheet = (Excel.Worksheet)workbook.Sheets.Add(After: workbook.Sheets[workbook.Sheets.Count]); // Добавляем новый лист в конец
                    }

                 
                    // Задаем имя листа (очищаем от недопустимых символов и обрезаем до 31)
                    string routeName = routeGroup.Key.RouteName;
                    string safeSheetName = string.Join("_", routeName.Split(System.IO.Path.GetInvalidFileNameChars())).Replace(":", "-");
                    if (safeSheetName.Length > 31) safeSheetName = safeSheetName.Substring(0, 31);
                    if (string.IsNullOrWhiteSpace(safeSheetName)) safeSheetName = $"Route_{routeGroup.Key.RouteID}";
                    worksheet.Name = safeSheetName;

                    int currentRow = 1; 

                    // Заголовок листа
                    worksheet.Cells[currentRow, 1] = $"История рейсов: {routeName}";
                    Excel.Range headerRange = worksheet.Range[worksheet.Cells[currentRow, 1], worksheet.Cells[currentRow, 6]]; // Объединяем 6 ячеек
                    headerRange.Merge();
                    headerRange.Font.Bold = true;
                    headerRange.Font.Size = 14;
                    headerRange.HorizontalAlignment = Excel.XlHAlign.xlHAlignCenter;
                    currentRow += 2; // Пропускаем строку

                    // Заголовки столбцов
                    int startDataRow = currentRow; // Запоминаем начало строк с данными
                    worksheet.Cells[currentRow, 1] = "Дата";
                    worksheet.Cells[currentRow, 2] = "Время отпр.";
                    worksheet.Cells[currentRow, 3] = "Время приб.";
                    worksheet.Cells[currentRow, 4] = "Автобус №";
                    worksheet.Cells[currentRow, 5] = "Тип автобуса";
                    worksheet.Cells[currentRow, 6] = "Мест";
                    // Можно добавить еще столбцы: Пассажиров, Выручка и т.д., если нужно считать
                    Excel.Range titleRange = worksheet.Range[worksheet.Cells[currentRow, 1], worksheet.Cells[currentRow, 6]]; // Выделяем заголовки
                    titleRange.Font.Bold = true;
                    titleRange.Borders.LineStyle = Excel.XlLineStyle.xlContinuous; // Добавляем границы
                    titleRange.VerticalAlignment = Excel.XlVAlign.xlVAlignCenter;
                    currentRow++;

                 
                    foreach (var schedule in routeGroup.OrderBy(s => s.DepartureData).ThenBy(s => s.DepartureTime))
                    {
                  

                      
                        worksheet.Cells[currentRow, 1] = schedule.DepartureData;

                
                        worksheet.Cells[currentRow, 2] = schedule.DepartureTime.TotalDays;
                        worksheet.Cells[currentRow, 3] = schedule.ArrivalTime.TotalDays;

                   
                        worksheet.Cells[currentRow, 4] = schedule.Buses?.Number;
                        worksheet.Cells[currentRow, 5] = schedule.Buses?.Type;
                        worksheet.Cells[currentRow, 6] = schedule.Buses?.CountOfSeats;
                        currentRow++;
                    }

              
                    if (currentRow - 1 >= startDataRow) 
                    {
                        Excel.Range dateDataRange = worksheet.Range[worksheet.Cells[startDataRow, 1], worksheet.Cells[currentRow - 1, 1]];
                        dateDataRange.NumberFormat = "dd.MM.yyyy"; 

                        Excel.Range timeDataRange1 = worksheet.Range[worksheet.Cells[startDataRow, 2], worksheet.Cells[currentRow - 1, 2]];
                        timeDataRange1.NumberFormat = "hh:mm"; 

                        Excel.Range timeDataRange2 = worksheet.Range[worksheet.Cells[startDataRow, 3], worksheet.Cells[currentRow - 1, 3]];
                        timeDataRange2.NumberFormat = "hh:mm"; 

                    }

 
                    worksheet.Columns.AutoFit();

                    if (worksheet != null) Marshal.ReleaseComObject(worksheet);
                    worksheet = null;

                } 

           
                while (workbook.Sheets.Count > sheetIndex)
                {
                  
                    ((Excel.Worksheet)workbook.Sheets[workbook.Sheets.Count]).Delete();
                }


             
                SaveFileDialog saveFileDialog = new SaveFileDialog
                {
                    Filter = "Книга Excel (*.xlsx)|*.xlsx|Книга Excel 97-2003 (*.xls)|*.xls",
                    Title = "Сохранить отчет по рейсам",
                    FileName = $"BusSchedulesReport_{DateTime.Now:yyyyMMdd}.xlsx"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                
                    Excel.XlFileFormat fileFormat = saveFileDialog.FilterIndex == 1 ?
                        Excel.XlFileFormat.xlOpenXMLWorkbook : // .xlsx
                        Excel.XlFileFormat.xlWorkbookNormal;   // .xls

                    workbook.SaveAs(saveFileDialog.FileName, fileFormat, Type.Missing, Type.Missing,
                                    false, false, Excel.XlSaveAsAccessMode.xlNoChange,
                                    Excel.XlSaveConflictResolution.xlUserResolution, true,
                                    Type.Missing, Type.Missing, Type.Missing);
                    MessageBox.Show($"Отчет успешно сохранен в файл:\n{saveFileDialog.FileName}", "Экспорт завершен", MessageBoxButton.OK, MessageBoxImage.Information);
                }
              

            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при экспорте в Excel: {ex.Message}\n{ex.StackTrace}", "Ошибка экспорта", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
               
                if (workbook != null)
                {
                    workbook.Close(false, Type.Missing, Type.Missing); 
                    Marshal.ReleaseComObject(workbook);
                    workbook = null;
                }
                if (excelApp != null)
                {
                    excelApp.Quit();
                    Marshal.ReleaseComObject(excelApp);
                    excelApp = null;
                }
                // Запускаем сборку мусора 
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }

        }

        private void BtnExportBusesToWord_Click(object sender, RoutedEventArgs e)
        {
            List<Buses> allBusesWithSchedules;
            Dictionary<int, int> ticketsPerSchedule = new Dictionary<int, int>();

            try
            {
                using (var context = new BusStationEntities())
                {
                    allBusesWithSchedules = context.Buses
                        .Include(b => b.Schedules.Select(s => s.Routes))
                        .OrderBy(b => b.Number)
                        .ToList();

                    ticketsPerSchedule = context.Tickets
                        .GroupBy(t => t.ScheduleID)
                        .ToDictionary(g => g.Key, g => g.Count());
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке данных для экспорта: {ex.Message}", "Ошибка данных", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (!allBusesWithSchedules.Any())
            {
                MessageBox.Show("Нет данных по автобусам для экспорта.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            Word.Application wordApp = null;
            Word.Document document = null;
            object missing = Missing.Value;

            try
            {
                wordApp = new Word.Application();
                document = wordApp.Documents.Add(ref missing, ref missing, ref missing, ref missing);

                bool firstBus = true;

                foreach (var bus in allBusesWithSchedules)
                {
                    Word.Range contentRange = document.Content;

                    if (!firstBus)
                    {
                        object collapseDirection = Word.WdCollapseDirection.wdCollapseEnd;
                        contentRange.Collapse(ref collapseDirection);
                        contentRange.InsertBreak(Word.WdBreakType.wdPageBreak);
                    }
                    firstBus = false;

                    // Заголовок автобуса
                    Word.Paragraph paraBusHeader = document.Content.Paragraphs.Add(ref missing);
                    paraBusHeader.Range.Text = $"Автобус №: {bus.Number}";
                    paraBusHeader.Range.Font.Bold = 1;
                    paraBusHeader.Range.Font.Size = 16;
                    paraBusHeader.Format.SpaceAfter = 12;
                    paraBusHeader.Range.InsertParagraphAfter();

                    // Информация об автобусе
                    Word.Paragraph paraBusInfo = document.Content.Paragraphs.Add(ref missing);
                    paraBusInfo.Range.Font.Size = 12;
                    paraBusInfo.Format.SpaceAfter = 6;
                    paraBusInfo.Range.Text = $"Тип: {bus.Type ?? "-"}";
                    paraBusInfo.Range.InsertParagraphAfter();

                    paraBusInfo = document.Content.Paragraphs.Add(ref missing);
                    paraBusInfo.Range.Text = $"Количество мест: {bus.CountOfSeats}";
                    paraBusInfo.Range.InsertParagraphAfter();

                    int totalPassengers = 0;
                    if (bus.Schedules != null)
                    {
                        foreach (var schedule in bus.Schedules)
                        {
                            if (ticketsPerSchedule.TryGetValue(schedule.Id, out int count))
                                totalPassengers += count;
                        }
                    }

                    paraBusInfo = document.Content.Paragraphs.Add(ref missing);
                    paraBusInfo.Range.Text = $"Всего перевезено пассажиров (по билетам): {totalPassengers}";
                    paraBusInfo.Range.InsertParagraphAfter();
                    paraBusInfo.Range.InsertParagraphAfter();

                    // Таблица рейсов
                    if (bus.Schedules != null && bus.Schedules.Any())
                    {
                        Word.Paragraph paraTableTitle = document.Content.Paragraphs.Add(ref missing);
                        paraTableTitle.Range.Text = "История назначенных рейсов:";
                        paraTableTitle.Range.Font.Bold = 1;
                        paraTableTitle.Range.Font.Size = 12;
                        paraTableTitle.Format.SpaceAfter = 6;
                        paraTableTitle.Range.InsertParagraphAfter();

                        int numRows = bus.Schedules.Count + 1;
                        int numCols = 4;
                        object endOfDoc = "\\endofdoc";
                        Word.Range tableRange = document.Bookmarks.get_Item(ref endOfDoc).Range;
                        Word.Table scheduleTable = document.Tables.Add(tableRange, numRows, numCols, ref missing, ref missing);
                        scheduleTable.Borders.Enable = 1;
                        scheduleTable.Range.Font.Size = 11;

                        // Заголовки
                        scheduleTable.Cell(1, 1).Range.Text = "Дата";
                        scheduleTable.Cell(1, 2).Range.Text = "Время отпр.";
                        scheduleTable.Cell(1, 3).Range.Text = "Маршрут";
                        scheduleTable.Cell(1, 4).Range.Text = "Пасс.";
                        scheduleTable.Rows[1].Range.Font.Bold = 1;
                        scheduleTable.Rows[1].Range.ParagraphFormat.Alignment = Word.WdParagraphAlignment.wdAlignParagraphCenter;

                        int row = 2;
                        foreach (var schedule in bus.Schedules.OrderBy(s => s.DepartureData).ThenBy(s => s.DepartureTime))
                        {
                            scheduleTable.Cell(row, 1).Range.Text = schedule.DepartureData.ToString("dd.MM.yyyy");
                            scheduleTable.Cell(row, 2).Range.Text = schedule.DepartureTime.ToString(@"hh\:mm");
                            scheduleTable.Cell(row, 3).Range.Text = schedule.Routes?.RouteDescription ?? "N/A";

                            int passCount = ticketsPerSchedule.TryGetValue(schedule.Id, out int pCount) ? pCount : 0;
                            scheduleTable.Cell(row, 4).Range.Text = passCount.ToString();
                            scheduleTable.Cell(row, 4).Range.ParagraphFormat.Alignment = Word.WdParagraphAlignment.wdAlignParagraphCenter;
                            row++;
                        }
                    }
                    else
                    {
                        Word.Paragraph paraNoSched = document.Content.Paragraphs.Add(ref missing);
                        paraNoSched.Range.Text = "Нет назначенных рейсов для этого автобуса.";
                        paraNoSched.Range.InsertParagraphAfter();
                    }
                }

                SaveFileDialog saveFileDialog = new SaveFileDialog
                {
                    Filter = "Документ Word (*.docx)|*.docx|Документ Word 97-2003 (*.doc)|*.doc",
                    Title = "Сохранить отчет по автобусам",
                    FileName = $"BusReport_{DateTime.Now:yyyyMMdd}.docx"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    object fileName = saveFileDialog.FileName;
                    object fileFormat = saveFileDialog.FilterIndex == 1 ?
                        Word.WdSaveFormat.wdFormatXMLDocument :
                        Word.WdSaveFormat.wdFormatDocument;

                    document.SaveAs2(ref fileName, ref fileFormat, ref missing, ref missing, ref missing,
                                     ref missing, ref missing, ref missing, ref missing, ref missing,
                                     ref missing, ref missing, ref missing, ref missing, ref missing,
                                     ref missing);

                    MessageBox.Show($"Отчет успешно сохранен в файл:\n{fileName}", "Экспорт завершен", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при экспорте в Word: {ex.Message}\n{ex.StackTrace}", "Ошибка экспорта", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                object falseObj = false;
                if (document != null)
                {
                    document.Close(ref falseObj, ref missing, ref missing);
                    Marshal.ReleaseComObject(document);
                    document = null;
                }
                if (wordApp != null)
                {
                    wordApp.Quit(ref falseObj, ref missing, ref missing);
                    Marshal.ReleaseComObject(wordApp);
                    wordApp = null;
                }
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }

    }
}
