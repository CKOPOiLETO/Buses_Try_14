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
        private void LoadScheduleData()
        {
            try
            {
                // Получаем свежий контекст
                var currentContext = BusStationEntities.GetContext();
                // Явно подгружаем связанные данные Routes и Buses
                var scheduleList = currentContext.Schedules
                                                .Include(s => s.Routes)
                                                .Include(s => s.Buses)
                                                .OrderBy(s => s.DepartureData).ThenBy(s => s.DepartureTime) // Сортировка для порядка
                                                .ToList();

                // Устанавливаем источник данных для DataGrid (как просили - в коде)
                DGridSchedules.ItemsSource = scheduleList;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки данных: {ex.Message}\n\nВнутренняя ошибка:\n{ex.InnerException?.Message}",
                                "Ошибка базы данных", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Обновляем данные при появлении страницы на экране
        private void Page_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (Visibility == Visibility.Visible)
            {
                // Обновляем данные из БД при каждом показе страницы
                // Важно! Это обновит данные, если они были изменены на других страницах
                // Сбрасываем старый контекст, если он был статическим и долгоживущим
                // BusStationEntities._context = null; // Если GetContext() реально создает новый при _context == null
                // Мы будем использовать GetContext() для каждой операции, чтобы избежать проблем с кэшированием
                LoadScheduleData();
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
    }
}
