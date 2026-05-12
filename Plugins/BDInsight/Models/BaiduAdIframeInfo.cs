using Microsoft.Playwright;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BDInsight.Models
{
    public class BaiduAdIframeInfo
    {
        public string? Src { get; set; }

        /// <summary>
        /// 主页面中的 iframe 元素
        /// </summary>
        public IElementHandle IframeElement { get; set; } = default!;

        /// <summary>
        /// iframe 内部的 Frame 对象
        /// </summary>
        public IFrame Frame { get; set; } = default!;
    }


}
