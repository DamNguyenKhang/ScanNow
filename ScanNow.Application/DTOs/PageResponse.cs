using System;
using System.Collections.Generic;
using System.Text;

namespace ScanNow.Application.DTOs
{
    public class PageResponse
    {
        public int Page { get; set; }
        public int Size { get; set; }
        public int TotalItems { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalItems / Size);
    }
}
