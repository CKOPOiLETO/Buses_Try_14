using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Buses_Try_14
{
    public class BusScheduleReportItem
    {
        public int BusNumber { get; set; }
        public string BusType { get; set; }
        public int BusCapacity { get; set; }
        public string RouteDescription { get; set; } 
        public DateTime DepartureDateTime { get; set; }
    }
}
