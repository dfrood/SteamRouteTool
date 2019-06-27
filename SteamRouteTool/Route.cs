using System.Collections.Generic;

namespace SteamRouteTool
{
    public class Route
    {
        public string name;
        public List<string> ranges;
        public List<int> row_index;
        public string desc;
        public bool extended;
        public bool pw;
        public bool all_check;
    }
}
