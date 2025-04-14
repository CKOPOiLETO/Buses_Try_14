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

namespace Buses_Try_14
{
    /// <summary>
    /// Логика взаимодействия для AddEditSchedulePage.xaml
    /// </summary>
    public partial class AddEditSchedulePage : Page
    {
        private Schedules _currentSchedule = null;
        private bool _isEdit = false;
        // private BusStationEntities _context; // Используем GetContext() при сохранении

        // Конструктор принимает рейс для редактирования (или null для нового)
        public AddEditSchedulePage(Schedules selectedSchedule)
        {
            InitializeComponent();
            // _context = BusStationEntities.GetContext(); // Не храним долгоживущий

            if (selectedSchedule != null)
            {
                _currentSchedule = selectedSchedule;
                _isEdit = true;
                Title = "Редактировать рейс"; // Меняем заголовок окна
            }
            else
            {
                _currentSchedule = new Schedules
                {
                    // Устанавливаем значения по умолчанию, если нужно
                    DepartureData = DateTime.Today // Например, сегодняшняя дата
                };
                _isEdit = false;
                Title = "Добавить новый рейс";
            }

            // Устанавливаем DataContext для привязки ComboBox и DatePicker
            DataContext = _currentSchedule;

            LoadComboBoxes();

            // Устанавливаем текст в TextBox'ах для времени (если редактируем)
            if (_isEdit)
            {
                TextBoxDepartureTime.Text = _currentSchedule.DepartureTime.ToString(@"hh\:mm");
                TextBoxArrivalTime.Text = _currentSchedule.ArrivalTime.ToString(@"hh\:mm");
            }
        }

        // Загрузка данных для ComboBox'ов
        private void LoadComboBoxes()
        {
            try
            {
                var currentContext = BusStationEntities.GetContext();
                //ComboRoutes.ItemsSource = currentContext.Routes.OrderBy(r => r.DepartuePoint).ToList();
                // Сначала получаем ВСЕ маршруты из БД в виде списка
                var routesList = currentContext.Routes.ToList();

                // ТЕПЕРЬ сортируем полученный список В ПАМЯТИ, используя C# свойство RouteDescription
                ComboRoutes.ItemsSource = routesList.OrderBy(r => r.RouteDescription).ToList();

                ComboBuses.ItemsSource = currentContext.Buses.OrderBy(b => b.Number).ToList();

                // Если редактируем, устанавливаем выбранные элементы
                if (_isEdit)
                {
                    ComboRoutes.SelectedItem = _currentSchedule.Routes; // Связь должна быть загружена
                    ComboBuses.SelectedItem = _currentSchedule.Buses;   // Связь должна быть загружена
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки справочников: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        // Кнопка "Сохранить"
        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            StringBuilder errors = new StringBuilder();

            // --- Валидация ---
            if (ComboRoutes.SelectedItem == null)
                errors.AppendLine("Выберите маршрут.");
            else
                _currentSchedule.Routes = ComboRoutes.SelectedItem as Routes; // Обновляем объект из ComboBox

            if (ComboBuses.SelectedItem == null)
                errors.AppendLine("Выберите автобус.");
            else
                _currentSchedule.Buses = ComboBuses.SelectedItem as Buses; // Обновляем объект из ComboBox


            if (DatePickerDeparture.SelectedDate == null)
                errors.AppendLine("Выберите дату отправления.");
            else
                _currentSchedule.DepartureData = DatePickerDeparture.SelectedDate.Value;

            // Валидация времени отправления
            if (!TimeSpan.TryParseExact(TextBoxDepartureTime.Text, @"hh\:mm", CultureInfo.InvariantCulture, out TimeSpan departureTime))
            {
                errors.AppendLine("Введите корректное время отправления в формате ЧЧ:ММ (например, 09:30).");
            }
            else
            {
                _currentSchedule.DepartureTime = departureTime;
            }

            // Валидация времени прибытия
            if (!TimeSpan.TryParseExact(TextBoxArrivalTime.Text, @"hh\:mm", CultureInfo.InvariantCulture, out TimeSpan arrivalTime))
            {
                errors.AppendLine("Введите корректное время прибытия в формате ЧЧ:ММ (например, 15:45).");
            }
            else
            {
                _currentSchedule.ArrivalTime = arrivalTime;
            }

            // Проверка логики времени (например, прибытие не раньше отправления в тот же день)
            // Добавьте свою логику, если необходимо


            // Показываем ошибки, если они есть
            if (errors.Length > 0)
            {
                MessageBox.Show(errors.ToString(), "Ошибки валидации", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // --- Сохранение ---
            try
            {
                var currentContext = BusStationEntities.GetContext();

                if (!_isEdit) // Если это новый рейс
                {
                    // Устанавливаем ID связанных сущностей перед добавлением
                    _currentSchedule.RouteID = _currentSchedule.Routes.Id;
                    _currentSchedule.BusId = _currentSchedule.Buses.Id;

                    // Важно! Сбрасываем навигационные свойства перед добавлением,
                    // чтобы EF не пытался добавить уже существующие Route и Bus
                    _currentSchedule.Routes = null;
                    _currentSchedule.Buses = null;

                    currentContext.Schedules.Add(_currentSchedule);
                }
                else // Если это редактирование
                {
                    // Находим существующую запись в контексте по ID, если она еще не отслеживается
                    var scheduleInDb = currentContext.Schedules.Find(_currentSchedule.Id);
                    if (scheduleInDb != null)
                    {
                        // Обновляем свойства существующей записи
                        // Если DataContext был установлен на _currentSchedule, EF может уже отслеживать изменения
                        // Но для надежности можно скопировать значения:
                        // currentContext.Entry(scheduleInDb).CurrentValues.SetValues(_currentSchedule); - не сработает если нав. свойства разные

                        scheduleInDb.RouteID = (ComboRoutes.SelectedItem as Routes).Id;
                        scheduleInDb.BusId = (ComboBuses.SelectedItem as Buses).Id;
                        scheduleInDb.DepartureData = _currentSchedule.DepartureData;
                        scheduleInDb.DepartureTime = _currentSchedule.DepartureTime;
                        scheduleInDb.ArrivalTime = _currentSchedule.ArrivalTime;

                        // Указываем, что сущность была изменена (если она не отслеживалась)
                        currentContext.Entry(scheduleInDb).State = System.Data.Entity.EntityState.Modified;
                    }
                    else
                    {
                        // Этого не должно произойти при правильной передаче объекта
                        MessageBox.Show("Не удалось найти редактируемый рейс в базе данных.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }


                }

                // Сохраняем изменения
                currentContext.SaveChanges();
                MessageBox.Show("Информация о рейсе успешно сохранена!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                Manager.MainFrame.GoBack(); // Возвращаемся на предыдущую страницу (список рейсов)
            }
            catch (System.Data.Entity.Validation.DbEntityValidationException dbEx) // Сначала ловим ошибки валидации EF
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("Ошибка валидации при сохранении:");
                foreach (var validationErrors in dbEx.EntityValidationErrors)
                {
                    sb.AppendFormat("- Сущность типа \"{0}\" в состоянии \"{1}\" имеет ошибки валидации:\n",
                        validationErrors.Entry.Entity.GetType().Name, validationErrors.Entry.State);
                    foreach (var validationError in validationErrors.ValidationErrors)
                    {
                        sb.AppendFormat("-- Свойство: {0}, Ошибка: {1}\n",
                            validationError.PropertyName, validationError.ErrorMessage);
                    }
                }
                MessageBox.Show(sb.ToString(), "Ошибка валидации", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (System.Data.Entity.Infrastructure.DbUpdateException dbUpdateEx) // Затем ловим ошибки обновления БД
            {
                // Пытаемся добраться до самого глубокого внутреннего исключения (часто от SQL Server)
                Exception innerEx = dbUpdateEx;
                while (innerEx.InnerException != null)
                {
                    innerEx = innerEx.InnerException;
                }

                string fullMessage = $"Ошибка обновления базы данных:\n{dbUpdateEx.Message}\n\n";
                // Добавляем информацию из записей, вызвавших ошибку (если доступно)
                // foreach (var entry in dbUpdateEx.Entries) {
                //    fullMessage += $"Сущность: {entry.Entity.GetType().Name} в состоянии {entry.State}\n";
                // }
                fullMessage += $"\nСамая вложенная ошибка:\n{innerEx.Message}";


                MessageBox.Show(fullMessage, "Ошибка базы данных", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex) // Общий обработчик для других ошибок
            {
                // Желательно логировать полное исключение ex.ToString() для диагностики
                System.Diagnostics.Debug.WriteLine($"General Error Saving Data: {ex.ToString()}"); // Вывод в окно Output
                MessageBox.Show($"Произошла общая ошибка при сохранении: {ex.Message}",
                                "Непредвиденная ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}