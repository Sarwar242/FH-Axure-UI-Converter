using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Core.Models
{
    public class IdReplacementRuleUI
    {
        public String Component { get; set; }
        public string OldKeyword { get; set; }
        public string NewKeyword { get; set; }

        public IdReplacementRuleUI(string component, string oldKeyword, string newKeyword)
        {
            Component = component;
            OldKeyword = oldKeyword;
            NewKeyword = newKeyword;
        }

        public IdReplacementRuleUI() { }
    }
}
