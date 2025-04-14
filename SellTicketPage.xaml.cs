using System;
using System.Collections.Generic;
using System.Globalization;
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
    /// Логика взаимодействия для SellTicketPage.xaml
    /// </summary>
    public partial class SellTicketPage : Page
    {
        private int _scheduleId;
        private Schedules _scheduleInfo; // Храним информацию о рейсе для проверок

        public SellTicketPage(int scheduleId)
        {
            InitializeComponent();
            _scheduleId = scheduleId;
            // Используем Dispatcher.BeginInvoke для асинхронной загрузки,
            // чтобы UI успел отрисоваться перед потенциально долгой загрузкой
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() =>
            {
                LoadScheduleInfo();
                LoadPassengers();
            }));
        }

        // Загрузка информации о выбранном рейсе
        private void LoadScheduleInfo()
        {
            using (var context = new BusStationEntities())
            {
                _scheduleInfo = context.Schedules
                                       .Include(s => s.Routes)
                                       .Include(s => s.Buses)
                                       .FirstOrDefault(s => s.Id == _scheduleId);

                if (_scheduleInfo != null && _scheduleInfo.Routes != null && _scheduleInfo.Buses != null)
                {
                    // Рассчитываем актуальное количество свободных мест
                    int availableSeatsNow = CalculateAvailableSeats(_scheduleId); // Используем тот же метод
                    string seatsInfo = availableSeatsNow >= 0 ? $"Свободно мест: {availableSeatsNow}" : "Ошибка расчета мест";

                    TextBlockScheduleInfo.Text =
                        $"{_scheduleInfo.Routes.DepartuePoint} - {_scheduleInfo.Routes.Destination}\n" +
                        $"Дата: {_scheduleInfo.DepartureData:dd.MM.yyyy} Время: {_scheduleInfo.DepartureTime:hh\\:mm}\n" +
                        $"Автобус: №{_scheduleInfo.Buses.Number} (Всего мест: {_scheduleInfo.Buses.CountOfSeats}) | {seatsInfo}";

                    // Блокируем кнопку, если мест нет или была ошибка расчета
                    BtnConfirmPurchase.IsEnabled = availableSeatsNow > 0;

                }
                else
                {
                    TextBlockScheduleInfo.Text = "Не удалось загрузить информацию о рейсе.";
                    BtnConfirmPurchase.IsEnabled = false; // Блокируем продажу
                }
            }
        }

        // Вспомогательный метод для расчета свободных мест (дублируем или выносим в общий класс)
        // Важно пересчитывать здесь, т.к. ситуация могла измениться пока открыта эта страница
        private int CalculateAvailableSeats(int scheduleId)
        {
            try
            {
                using (var context = new BusStationEntities())
                {
                    var schedule = context.Schedules
                                        .Include(s => s.Buses)
                                        .FirstOrDefault(s => s.Id == scheduleId);
                    if (schedule == null || schedule.Buses == null) return -1;
                    int totalSeats = schedule.Buses.CountOfSeats;
                    int soldSeats = context.Tickets.Count(t => t.ScheduleID == scheduleId);
                    return totalSeats - soldSeats;
                }
            }
            catch { return -1; } // Просто возвращаем ошибку
        }


        // Загрузка списка пассажиров в ComboBox
        private void LoadPassengers()
        {
            using (var context = new BusStationEntities())
            {
                try
                {
                    var passengers = context.Passangers
                                            .OrderBy(p => p.LastName)
                                            .ThenBy(p => p.FirstName)
                                            .ToList(); // Загружаем всех
                    ComboPassengers.ItemsSource = passengers;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка загрузки списка пассажиров: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // Переход на страницу добавления нового пассажира
        private void BtnAddNewPassenger_Click(object sender, RoutedEventArgs e)
        {
            Manager.MainFrame.Navigate(new AddEditPassengerPage(null));
            // При возвращении нужно будет обновить список пассажиров
            // Простой способ - перезагрузить при активации окна, но лучше сделать
            // через событие или передачу данных обратно.
            // Пока оставим так: пользователь добавит, вернется, список обновится при след. открытии.
            // Или обновим список после навигации GoBack() если сможем это отследить.
        }


        // Валидация ввода цены (только цифры и разделитель)
        private void TextBoxPrice_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            TextBox textBox = sender as TextBox;
            string currentText = textBox.Text.Remove(textBox.SelectionStart, textBox.SelectionLength).Insert(textBox.CaretIndex, e.Text);

            // Разрешаем цифры и один десятичный разделитель (запятая или точка)
            // Проверяем, что в тексте не более одного разделителя
            bool isDecimalSeparator = e.Text == "." || e.Text == ",";
            int separatorCount = currentText.Count(c => c == '.' || c == ',');

            if (!char.IsDigit(e.Text, e.Text.Length - 1) && // Не цифра
                 !isDecimalSeparator || // и не разделитель
                 (isDecimalSeparator && separatorCount > 1) || // или разделитель, но он уже не первый
                 (isDecimalSeparator && textBox.Text.Contains(".") || textBox.Text.Contains(","))) // или разделитель, но он уже есть
            {
                e.Handled = true; // Игнорируем ввод
            }
        }

        /* // Валидация для номера места (если добавите)
        private void TextBoxSeat_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !e.Text.All(char.IsDigit); // Разрешаем только цифры
        }
        */


        // Кнопка "Оформить билет"
        private void BtnConfirmPurchase_Click(object sender, RoutedEventArgs e)
        {
            // --- Валидация ---
            StringBuilder errors = new StringBuilder();
            Passangers selectedPassenger = ComboPassengers.SelectedItem as Passangers;
            decimal price;
            // int seatNumber = 0; // Если добавите место

            if (selectedPassenger == null)
                errors.AppendLine("Выберите пассажира.");

            // Используем CultureInfo.InvariantCulture для надежности при парсинге с точкой или запятой
            if (!decimal.TryParse(TextBoxPrice.Text.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out price) || price <= 0)
                errors.AppendLine("Введите корректную положительную цену билета.");

            /* // Валидация места (если добавите)
            if (!int.TryParse(TextBoxSeat.Text, out seatNumber) || seatNumber <= 0)
                 errors.AppendLine("Введите корректный номер места.");
            else if (_scheduleInfo != null && seatNumber > _scheduleInfo.Buses.CountOfSeats)
                 errors.AppendLine($"Номер места не может быть больше {_scheduleInfo.Buses.CountOfSeats}.");
            // !!! Также нужна проверка, не занято ли уже это место на рейсе !!!
            */


            // Проверка наличия мест (повторно)
            int availableSeats = CalculateAvailableSeats(_scheduleId);
            if (availableSeats <= 0)
            {
                errors.AppendLine("На рейс не осталось свободных мест (возможно, их только что купили).");
            }


            if (errors.Length > 0)
            {
                MessageBox.Show(errors.ToString(), "Ошибки", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // --- Создание и сохранение билета ---
            var newTicket = new Tickets
            {
                ScheduleID = _scheduleId,
                PassengerID = selectedPassenger.Id,
                PurchaseDate = DateTime.Now, // Текущая дата и время покупки
                Price = price
                // SeatNumber = seatNumber; // Если добавите
            };

            try
            {
                using (var context = new BusStationEntities())
                {
                    context.Tickets.Add(newTicket);
                    context.SaveChanges(); // Сохраняем билет

                    MessageBox.Show($"Билет для {selectedPassenger.FullName} успешно оформлен!\nЦена: {price:C}", // :C для формата валюты
                                    "Билет продан", MessageBoxButton.OK, MessageBoxImage.Information);

                    // Обновляем информацию о рейсе на этой же странице (кол-во мест)
                    LoadScheduleInfo();

                    // Очищаем поля для возможной продажи следующего билета на этот же рейс
                    // ComboPassengers.SelectedIndex = -1; // Сброс выбора пассажира
                    // TextBoxPrice.Clear();
                    // TextBoxSeat.Clear(); // Если есть

                    // Или просто возвращаемся назад
                    Manager.MainFrame.GoBack();
                }
            }
            catch (Exception ex) // Используем улучшенный обработчик ошибок
            {
                HandleSaveChangesException(ex);
            }
        }

        // Вспомогательный метод для обработки ошибок SaveChanges (можно вынести в отдельный класс)
        private void HandleSaveChangesException(Exception ex)
        {
            if (ex is System.Data.Entity.Validation.DbEntityValidationException dbEx) { /*...*/ } // Копируйте код из AddEditPassengerPage
            else if (ex is System.Data.Entity.Infrastructure.DbUpdateException dbUpdateEx) { /*...*/ } // Копируйте код из AddEditPassengerPage
            else { /*...*/ } // Копируйте код из AddEditPassengerPage
        }

    }

}
