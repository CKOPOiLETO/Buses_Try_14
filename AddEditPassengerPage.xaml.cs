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

namespace Buses_Try_14
{
    /// <summary>
    /// Логика взаимодействия для AddEditPassengerPage.xaml
    /// </summary>
    public partial class AddEditPassengerPage : Page
    {
        private Passangers _currentPassenger = null;
        private bool _isEdit = false;

        public AddEditPassengerPage(Passangers selectedPassenger)
        {
            InitializeComponent();

            if (selectedPassenger != null)
            {
                _currentPassenger = selectedPassenger;
                _isEdit = true;
                Title = "Редактировать пассажира";
            }
            else
            {
                _currentPassenger = new Passangers();
                _isEdit = false;
                Title = "Добавить пассажира";
            }

            DataContext = _currentPassenger; // Устанавливаем контекст для привязки TextBox'ов
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            StringBuilder errors = new StringBuilder();

            // Валидация (добавьте свои правила, если нужно)
            if (string.IsNullOrWhiteSpace(_currentPassenger.LastName))
                errors.AppendLine("Укажите фамилию.");
            if (string.IsNullOrWhiteSpace(_currentPassenger.FirstName))
                errors.AppendLine("Укажите имя.");
            // Телефон и отчество могут быть необязательными

            if (errors.Length > 0)
            {
                MessageBox.Show(errors.ToString(), "Ошибки валидации", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using (var context = new BusStationEntities())
                {
                    if (!_isEdit) // Добавление нового
                    {
                        context.Passangers.Add(_currentPassenger);
                    }
                    else // Редактирование существующего
                    {
                        // Находим запись в текущем контексте и помечаем как измененную
                        var passengerInDb = context.Passangers.Find(_currentPassenger.Id);
                        if (passengerInDb != null)
                        {
                            // Копируем значения из _currentPassenger в passengerInDb
                            context.Entry(passengerInDb).CurrentValues.SetValues(_currentPassenger);
                            // Или вручную:
                            // passengerInDb.LastName = _currentPassenger.LastName;
                            // passengerInDb.FirstName = _currentPassenger.FirstName;
                            // ... и т.д.

                            context.Entry(passengerInDb).State = System.Data.Entity.EntityState.Modified;
                        }
                        else
                        {
                            // Этого не должно произойти при правильной работе
                            MessageBox.Show("Не удалось найти редактируемого пассажира в базе данных.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }
                    }

                    context.SaveChanges();
                    MessageBox.Show("Информация о пассажире сохранена.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    Manager.MainFrame.GoBack(); // Возвращаемся к списку
                }
            }
            catch (System.Data.Entity.Validation.DbEntityValidationException dbEx)
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("Ошибка валидации при сохранении:");
                foreach (var validationErrors in dbEx.EntityValidationErrors) { /* ... (код как в предыдущем ответе) ... */ }
                MessageBox.Show(sb.ToString(), "Ошибка валидации", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (System.Data.Entity.Infrastructure.DbUpdateException dbUpdateEx)
            {
                Exception innerEx = dbUpdateEx;
                while (innerEx.InnerException != null) { innerEx = innerEx.InnerException; }
                MessageBox.Show($"Ошибка обновления БД:\n{dbUpdateEx.Message}\n\nВнутренняя ошибка:\n{innerEx.Message}", "Ошибка базы данных", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"General Error Saving Passenger: {ex.ToString()}");
                MessageBox.Show($"Общая ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
