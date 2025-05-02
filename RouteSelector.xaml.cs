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
using System.Collections.ObjectModel; // Для ObservableCollection
using System.ComponentModel; // Для INotifyPropertyChanged
using System.Runtime.CompilerServices; // Для CallerMemberName
using System.Data.Entity; // Для доступа к БД

namespace Buses_Try_14 // Убедитесь, что пространство имен ваше
{
    public partial class RouteSelector : UserControl, INotifyPropertyChanged
    {
        // --- Свойства для привязки к ComboBox ---
        public ObservableCollection<string> DeparturePoints { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> DestinationPoints { get; } = new ObservableCollection<string>();

        private string _selectedDeparturePoint;

        public string SelectedDeparturePoint
        {
            get => _selectedDeparturePoint;
            set { SetProperty(ref _selectedDeparturePoint, value); FindMatchingRoute(); } // Ищем маршрут при изменении
        }

        private string _selectedDestinationPoint;
       

        public string SelectedDestinationPoint
        {
            get => _selectedDestinationPoint;
            set { SetProperty(ref _selectedDestinationPoint, value); FindMatchingRoute(); } // Ищем маршрут при изменении
        }

        // --- Свойство для хранения найденного маршрута ---
        private Routes _selectedRoute;
        public Routes SelectedRoute
        {
            get => _selectedRoute;
            private set { SetProperty(ref _selectedRoute, value); UpdateRouteInfoText(); RouteSelected?.Invoke(this, value); } // Обновляем текст и вызываем событие
        }

        // --- Свойство для отображения информации о маршруте ---
        private string _routeInfoText = "Выберите пункты отправления и назначения";
        public string RouteInfoText
        {
            get => _routeInfoText;
            private set { SetProperty(ref _routeInfoText, value); }
        }

        // --- Событие, возникающее при выборе валидного маршрута ---
        // Передаем выбранный объект Routes (или null, если маршрут не найден/сброшен)
        public event EventHandler<Routes> RouteSelected;


        // --- Конструктор и Загрузка данных ---
        public RouteSelector()
        {
            InitializeComponent();
            this.DataContext = this; // Устанавливаем DataContext для привязок в XAML
            if (!DesignerProperties.GetIsInDesignMode(this)) // Не загружаем данные в дизайнере
            {
                LoadDistinctPoints();
            }
        }

        private async void LoadDistinctPoints()
        {
            // Очищаем текущие списки
            DeparturePoints.Clear();
            DestinationPoints.Clear();

            try
            {
                using (var context = new BusStationEntities())
                {
                    // Асинхронно загружаем уникальные пункты
                    var departures = await context.Routes
                                                .Select(r => r.DepartuePoint)
                                                .Distinct()
                                                .OrderBy(p => p)
                                                .ToListAsync();

                    var destinations = await context.Routes
                                                  .Select(r => r.Destination)
                                                  .Distinct()
                                                  .OrderBy(p => p)
                                                  .ToListAsync();

                    // Заполняем ObservableCollections (в UI потоке)
                    foreach (var point in departures) DeparturePoints.Add(point);
                    foreach (var point in destinations) DestinationPoints.Add(point);
                }
            }
            catch (Exception ex)
            {
                // Отображаем ошибку пользователю или логируем
                RouteInfoText = "Ошибка загрузки списка маршрутов.";
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки точек маршрутов: {ex.Message}");
            }
        }

        // --- Поиск маршрута при изменении выбора ---
        private async void FindMatchingRoute()
        {
            // Ищем только если выбраны оба пункта
            if (!string.IsNullOrWhiteSpace(SelectedDeparturePoint) && !string.IsNullOrWhiteSpace(SelectedDestinationPoint))
            {
                try
                {
                    using (var context = new BusStationEntities())
                    {
                        // Ищем точное совпадение маршрута
                        var foundRoute = await context.Routes.FirstOrDefaultAsync(r =>
                            r.DepartuePoint == SelectedDeparturePoint &&
                            r.Destination == SelectedDestinationPoint);

                        SelectedRoute = foundRoute; // Устанавливаем найденный маршрут (или null, если не найден)
                    }
                }
                catch (Exception ex)
                {
                    SelectedRoute = null; // Сбрасываем в случае ошибки
                    RouteInfoText = "Ошибка при поиске маршрута.";
                    System.Diagnostics.Debug.WriteLine($"Ошибка поиска маршрута: {ex.Message}");
                }
            }
            else
            {
                // Если один из пунктов не выбран, сбрасываем результат
                SelectedRoute = null;
            }
        }

        // Обновление текста с информацией о маршруте
        private void UpdateRouteInfoText()
        {
            if (SelectedRoute != null)
            {
                // Используем свойство RouteDescription из частичного класса Routes
                RouteInfoText = $"Найден маршрут: {SelectedRoute.RouteDescription}";
            }
            else if (!string.IsNullOrWhiteSpace(SelectedDeparturePoint) && !string.IsNullOrWhiteSpace(SelectedDestinationPoint))
            {
                // Если оба пункта выбраны, но маршрут не найден
                RouteInfoText = "Такой прямой маршрут не найден.";
            }
            else
            {
                // Если не все пункты выбраны
                RouteInfoText = "Выберите пункты отправления и назначения";
            }
        }


        // --- Реализация INotifyPropertyChanged ---
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(storage, value)) return false;
            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        // --- Конец INotifyPropertyChanged ---
    }
}
