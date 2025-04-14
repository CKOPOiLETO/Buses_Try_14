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
using System.Data.Entity;

namespace Buses_Try_14
{
    /// <summary>
    /// Логика взаимодействия для ViewPassengersOnSchedulePage.xaml
    /// </summary>
    public partial class ViewPassengersOnSchedulePage : Page
    {
        private int _scheduleId;

        public ViewPassengersOnSchedulePage(int scheduleId)
        {
            InitializeComponent();
            _scheduleId = scheduleId;
            LoadData(); // Загружаем данные при создании страницы
        }

        private void LoadData()
        {
            using (var context = new BusStationEntities())
            {
                try
                {
                    // 1. Загружаем информацию о самом рейсе для заголовка
                    // Используем Include, чтобы достать названия пунктов маршрута
                    var scheduleInfo = context.Schedules
                                              .Include(s => s.Routes) // Подгружаем маршрут
                                              .FirstOrDefault(s => s.Id == _scheduleId);

                    if (scheduleInfo != null && scheduleInfo.Routes != null)
                    {
                        // Устанавливаем заголовок окна и текст в TextBlock
                        string routeName = $"{scheduleInfo.Routes.DepartuePoint} - {scheduleInfo.Routes.Destination}";
                        Title = $"Пассажиры на рейс: {routeName}";
                        TextBlockScheduleHeader.Text = $"Рейс: {routeName}\n" +
                                                       $"Дата отправления: {scheduleInfo.DepartureData:dd.MM.yyyy} {scheduleInfo.DepartureTime:hh\\:mm}";
                    }
                    else
                    {
                        TextBlockScheduleHeader.Text = "Не удалось загрузить информацию о рейсе.";
                    }

                    // 2. Загружаем билеты на этот рейс, обязательно включая данные пассажиров
                    var ticketsOnSchedule = context.Tickets
                                                   .Include(t => t.Passangers) // <-- ВАЖНО: подгружаем пассажира!
                                                   .Where(t => t.ScheduleID == _scheduleId)
                                                   .OrderBy(t => t.Passangers.LastName) // Сортируем по фамилии пассажира
                                                   .ThenBy(t => t.Passangers.FirstName)
                                                   .ToList();

                    // Устанавливаем источник данных для DataGrid
                    DGridPassengerTickets.ItemsSource = ticketsOnSchedule;

                    // Если пассажиров нет, можно вывести сообщение (опционально)
                    if (!ticketsOnSchedule.Any())
                    {
                        // Можно добавить TextBlock на страницу и сделать его видимым,
                        // или просто оставить грид пустым.
                        // TextBlockNoPassengersMessage.Visibility = Visibility.Visible;
                    }

                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка загрузки данных: {ex.Message}\n\nВнутренняя ошибка:\n{ex.InnerException?.Message}",
                                    "Ошибка базы данных", MessageBoxButton.OK, MessageBoxImage.Error);
                    TextBlockScheduleHeader.Text = "Ошибка загрузки данных.";
                    DGridPassengerTickets.ItemsSource = null;
                }
            }
        }
    }
}
