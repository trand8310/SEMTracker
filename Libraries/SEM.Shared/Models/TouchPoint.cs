
namespace SEM.Models
{
    public class TouchPoint
    {
        public TouchPoint(int x, int y)
        {
            this.x = x; 
            this.y = y;

        }
        public int x { get; set; }
        public int y { get; set; }
    }
}
