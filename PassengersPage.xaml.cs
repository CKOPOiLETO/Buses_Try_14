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
    /// Логика взаимодействия для PassengersPage.xaml
    /// </summary>
    public partial class PassengersPage : Page
    {
        public PassengersPage()
        {
            InitializeComponent();
        }

        private void LoadPassengersData(string searchLastName = null)
        {
            using (var context = new BusStationEntities())
            {
                try
                {
                    IQueryable<Passangers> query = context.Passangers;

                    // Фильтрация по фамилии, если указана
                    if (!string.IsNullOrWhiteSpace(searchLastName))
                    {
                        query = query.Where(p => p.LastName.Contains(searchLastName));
                    }

                    // Выполняем запрос и сортируем
                    var passengersList = query.OrderBy(p => p.LastName).ThenBy(p => p.FirstName).ToList();
                    DGridPassengers.ItemsSource = passengersList;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка загрузки данных пассажиров: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    DGridPassengers.ItemsSource = null;
                }
            }
        }

        private void Page_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (Visibility == Visibility.Visible)
            {
                // Загружаем данные при каждом показе страницы
                LoadPassengersData(TxtSearchLastName.Text); // Учитываем текущий поиск
            }
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            // Переход на страницу добавления/редактирования с null (новый пассажир)
            Manager.MainFrame.Navigate(new AddEditPassengerPage(null));
        }

        private void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            var selectedPassenger = (sender as Button)?.DataContext as Passangers;
            if (selectedPassenger != null)
            {
                Manager.MainFrame.Navigate(new AddEditPassengerPage(selectedPassenger));
            }
        }

        private void BtnSearch_Click(object sender, RoutedEventArgs e)
        {
            LoadPassengersData(TxtSearchLastName.Text);
        }

        private void BtnClearSearch_Click(object sender, RoutedEventArgs e)
        {
            TxtSearchLastName.Text = "";
            LoadPassengersData(); // Загрузить всех
        }

        // --- Логика для удаления (если решите добавить кнопку) ---
        /*
        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            var passengerToDelete = DGridPassengers.SelectedItem as Passangers;

            if (passengerToDelete == null)
            {
                MessageBox.Show("Выберите пассажира для удаления.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // !!! ВАЖНО: Проверка наличия билетов у пассажира перед удалением !!!
            using (var context = new BusStationEntities())
            {
                bool hasTickets = context.Tickets.Any(t => t.PassengerID == passengerToDelete.Id);
                if (hasTickets)
                {
                    MessageBox.Show("Невозможно удалить пассажира, так как у него есть купленные билеты.", "Ошибка удаления", MessageBoxButton.OK, MessageBoxImage.Stop);
                    return;
                }
            }


            if (MessageBox.Show($"Вы уверены, что хотите удалить пассажира {passengerToDelete.LastName} {passengerToDelete.FirstName}?",
                                "Подтверждение удаления", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                try
                {
                    using (var context = new BusStationEntities())
                    {
                        // Находим пассажира в текущем контексте и удаляем
                        var passengerInDb = context.Passangers.Find(passengerToDelete.Id);
                        if (passengerInDb != null)
                        {
                             context.Passangers.Remove(passengerInDb);
                             context.SaveChanges();
                             MessageBox.Show("Пассажир успешно удален.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                             LoadPassengersData(TxtSearchLastName.Text); // Обновляем список
                        }
                    }
                }
                catch (Exception ex)
                {
                     MessageBox.Show($"Ошибка при удалении пассажира: {ex.Message}", "Ошибка базы данных", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        */
    }
}
