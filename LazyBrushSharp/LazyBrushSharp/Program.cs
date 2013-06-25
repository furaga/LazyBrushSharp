using System;

namespace LazyBrushSharp
{
#if WINDOWS || XBOX
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main(string[] args)
        {
            using (GameLazyBrush game = new GameLazyBrush())
            {
                game.Run();
            }
        }
    }
#endif
}

