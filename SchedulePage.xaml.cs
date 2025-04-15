using System;
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
        private void LoadScheduleData(string departureFilter = null, string destinationFilter = null, DateTime? dateFilter = null)
        {
            using (var context = new BusStationEntities())
            {
                try
                {
                    // Начинаем с базового запроса, включая связанные сущности
                    IQueryable<Schedules> query = context.Schedules
                                                         .Include(s => s.Routes)
                                                         .Include(s => s.Buses);

                    // Применяем фильтр по пункту отправления (по подстроке, без учета регистра)
                    if (!string.IsNullOrWhiteSpace(departureFilter))
                    {
                        string lowerDepartureFilter = departureFilter.ToLower();
                        query = query.Where(s => s.Routes.DepartuePoint.ToLower().Contains(lowerDepartureFilter));
                    }

                    // Применяем фильтр по пункту назначения (по подстроке, без учета регистра)
                    if (!string.IsNullOrWhiteSpace(destinationFilter))
                    {
                        string lowerDestinationFilter = destinationFilter.ToLower();
                        query = query.Where(s => s.Routes.Destination.ToLower().Contains(lowerDestinationFilter));
                    }

                    // Применяем фильтр по дате (точное совпадение дня, месяца, года)
                    if (dateFilter.HasValue)
                    {
                        DateTime dateToFilter = dateFilter.Value.Date; // Убираем время из выбранной даты
                        // Используем DbFunctions.TruncateTime для сравнения только дат в базе данных
                        query = query.Where(s => DbFunctions.TruncateTime(s.DepartureData) == dateToFilter);
                    }

                    // Выполняем итоговый запрос с сортировкой
                    var scheduleList = query.OrderBy(s => s.DepartureData) // Сортируем по дате
                                            .ThenBy(s => s.DepartureTime) // Затем по времени
                                            .ToList();

                    // Устанавливаем результат как источник данных для DataGrid
                    DGridSchedules.ItemsSource = scheduleList;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка загрузки данных: {ex.Message}\n\nВнутренняя ошибка:\n{ex.InnerException?.Message}",
                                    "Ошибка базы данных", MessageBoxButton.OK, MessageBoxImage.Error);
                    DGridSchedules.ItemsSource = null; // Очищаем грид в случае ошибки
                }
            }
        }

        // Обновляем данные при появлении страницы на экране
        private void Page_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (Visibility == Visibility.Visible)
            {
                // Загружаем данные с учетом текущих значений в полях фильтров
                LoadScheduleData(TxtFilterDeparture.Text, TxtFilterDestination.Text, DpFilterDate.SelectedDate);
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
            LoadScheduleData(TxtFilterDeparture.Text, TxtFilterDestination.Text, DpFilterDate.SelectedDate);
        }

        private void BtnResetFilters_Click(object sender, RoutedEventArgs e)
        {
            // Очищаем поля ввода фильтров
            TxtFilterDeparture.Text = "";
            TxtFilterDestination.Text = "";
            DpFilterDate.SelectedDate = null; // Сбрасываем выбранную дату

            // Загружаем все данные без фильтров
            LoadScheduleData();
        }

        private void BtnGoToReport_Click(object sender, RoutedEventArgs e)
        {
            Manager.MainFrame.Navigate(new ReportPage());
        }
    }
}
