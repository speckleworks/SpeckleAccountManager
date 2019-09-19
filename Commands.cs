using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;

namespace SpecklePopup
{
  public static class Commands
  {
    public static readonly RoutedUICommand RemoveAccount = new RoutedUICommand("RemoveAccount", "RemoveAccount", typeof(Button));
  }
}
