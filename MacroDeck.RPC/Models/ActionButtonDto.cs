using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MacroDeck.RPC.Models;

public class ActionButtonDto
{
    public string BackgroundColorHex { get; set; }
    public string BackgroundBase64Hex { get; set; }
    public string LabelBase64 { get; set; }
    public bool State { get; set; }
}