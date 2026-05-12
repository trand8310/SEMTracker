using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SEM
{
    public class TaskAdWordEventArgs
    {
        public string Type { get; set; }
        public string Word { get; set; }

        public TaskAdWordEventArgs(string type, string word)
        {
            Type = type;
            Word = word;
        }
    }
}
