﻿using GoRogue;
using GoRogue.MapViews;
using GoRogue.SenseMapping;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using SadConsole;
using StarsHollow.Utils;
using StarsHollow.World;
using System;
using System.Collections;
using StarsHollow.Engine;

namespace StarsHollow.UserInterface
{
    /* 
    UIManager takes care of representing all things graphical. It's children
    are the MainWindow, and the consoles inside it: Map, Log, Status.
    Also includes color settings, themes, fonts and so on.
    */
    class UI
    {
        //  public SadConsole.Themes.Library library;

        private readonly int _width;
        private readonly int _height;
        public readonly MainWindow MainWindow;
        private Map _currentMap;
        private MessageLogWindow _messageLogWindow;
        public readonly WorldMap _world;


        public UI(int screenWidth, int screenHeight, WorldMap world, MainLoop mainLoop)
        {
            _width = screenWidth;
            _height = screenHeight;
            SetupLook();
            MainWindow = new MainWindow(_width, _height, world, mainLoop, _messageLogWindow);
            _world = world;
        }

        public void AddMessage(string message)
        {
            MainWindow.Message(message);
        }
        private void SetupLook()
        {
            // Theme
            SadConsole.Themes.WindowTheme windowTheme = new SadConsole.Themes.WindowTheme();
            windowTheme.BorderLineStyle = CellSurface.ConnectedLineThick;
            SadConsole.Themes.Library.Default.WindowTheme = windowTheme;
            SadConsole.Themes.Library.Default.Colors.TitleText = ColorScheme.Three;
            SadConsole.Themes.Library.Default.Colors.Lines = ColorScheme.Three;
            SadConsole.Themes.Library.Default.Colors.ControlHostBack = ColorScheme.First;
        }
    }

    // MainWindow Creates and holds every other window inside it.
    // 
    internal class MainWindow : ContainerConsole
    {

        private IEnumerator _iterator;
        private readonly WorldMap _world;

        private States _gameState = States.StartMenu;

        public States GameState
        {
            get => _gameState;
            set => _gameState = value;
        }

        private Window _menuWindow;
        private ScrollingConsole _menuConsole;

        private Window _mapWindow;
        private ScrollingConsole _mapConsole;

        private MessageLogWindow _messageLogWindow;

        private readonly MainLoop _mainLoop;

        private FOV _fov;

        public void Message(string message)
        {
            _messageLogWindow.Add(message);
        }

        
        public MainWindow(int width, int height, WorldMap world, MainLoop mainLoop, MessageLogWindow messageLogWindow)
        {
            _world = world;
            _messageLogWindow = messageLogWindow;
            IsVisible = true;
            IsFocused = true;

            Parent = Global.CurrentScreen;
            CreateWindowsAndConsoles(width, height);
            _menuWindow.Show();

            _mainLoop = mainLoop;
            MainLoop.onTurnChange += ChangeState;

            StartGame();
        }

        public override void Update(TimeSpan timeElapsed)
        {
            switch (_gameState)
            {
                case States.StartMenu:
                    StartMenuKeyboard();
                    break;
                case States.Input:
                    WorldMapKeyboard();
                    break;
                case States.Main:
                {
                    if (_iterator == null)
                    {
                        _iterator = _mainLoop.Loop().GetEnumerator();
                    }
                    _iterator.MoveNext();
                    break;
                }
            }
            CheckMouse();
            base.Update(timeElapsed);
        }
        // ============INIT==========================================
        private void CreateWindowsAndConsoles(int width, int height)
        {
            // calculating sizes of the child windows and consoles
            double tempWidth = width / 1.5 / 1.618;
            double tempHeight = height * 1.5 / 1.618;
            // but hardcoded values used here.
            int _mapWidth = 72; //Convert.ToInt32(_tempWidth);
            int _mapHeight = 40;  //Convert.ToInt32(_tempHeight);

            // Consoles
            _menuConsole = new ScrollingConsole(width, height, Fonts.quarterSizeFont);
            _mapConsole = new ScrollingConsole(_mapWidth, _mapHeight, Fonts.halfSizeFont);

            // Windows
            CreateMenuWindow();
            CreateMapWindow(_mapWidth, _mapHeight, "*Stars Hollow*");
            CreateMessageLogWindow();

            // Creators for windows 
            void CreateMenuWindow()
            {
                _menuWindow = new Window(width, height);
                // load image from REXpaint file.
                ScrollingConsole rexConsole;

                using (var rexStream = System.IO.File.OpenRead(@"../../../res/xp/metsa.xp"))
                {
                    var rex = SadConsole.Readers.REXPaintImage.Load(rexStream);
                    rexConsole = rex.ToLayeredConsole();
                }

                rexConsole.Position = new Point(0, 0);
                rexConsole.Font = Fonts.quarterSizeFont;

                _menuWindow.Children.Add(rexConsole);
                Children.Add(_menuWindow);
            }
            void CreateMapWindow(int mapWidth, int mapHeight, string title)
            {
                _mapWindow = new Window(mapWidth, mapHeight);
                _mapConsole = new ScrollingConsole(_mapWindow.Width, _mapWindow.Height, Fonts.halfSizeFont,
                                                        new Microsoft.Xna.Framework.Rectangle(0, 0, Width, Height));
                //make console short enough to show the window title
                //and borders, and position it away from borders
                int mapConsoleWidth = mapWidth - 2;
                int mapConsoleHeight = mapHeight - 2;

                // Resize the Map Console's ViewPort to fit inside of the window's borders
                _mapConsole.ViewPort = new Microsoft.Xna.Framework.Rectangle(0, 0, mapConsoleWidth, mapConsoleHeight);
                //reposition the MapConsole so it doesnt overlap with the left/top window edges
                _mapConsole.Position = new Point(1, 1);
                //TargetConsole.Position = new Point(1, 1);

                // Centre the title text at the top of the window
                _mapWindow.Title = title.Align(HorizontalAlignment.Center, mapConsoleWidth, (char)205);

                //add the map viewer to the window
                _mapWindow.Children.Add(_mapConsole);

                // The MapWindow becomes a child console of the MainWindow
                Children.Add(_mapWindow);

                _mapWindow.Font = Fonts.halfSizeFont;
                _mapConsole.Font = Fonts.halfSizeFont;

            }
            void CreateMessageLogWindow()
            {

                _messageLogWindow = new MessageLogWindow(_mapWidth, height - _mapHeight + 15, "*LOG*")
                {
                    Font = Fonts.halfSizeFont
                };
                Children.Add(_messageLogWindow);
                _messageLogWindow.Position = new Point(0, _mapHeight);
                _messageLogWindow.Show();
            }
        }


        // ============WINDOW & CONSOLE MANAGEMENT==========================================

        private void DisplayFOV()
        {
            _world.CurrentMap._tiles[_world.Player.Position.ToIndex(_world.CurrentMap._width)].fovMap.Calculate(_world.Player.Position, 55, Radius.SQUARE);
            foreach (Point pos in _world.CurrentMap.goMap.Positions())
            {
                if (_world.CurrentMap._tiles[pos.ToIndex(_world.CurrentMap._width)].IsExplored)
                {
                    _world.CurrentMap._tiles[pos.ToIndex(_world.CurrentMap._width)].Foreground.A = 220;
                }

            }

            // set all currently visible tiles to their normal color
            // and entities Visible
            foreach (var pos in _world.CurrentMap._tiles[_world.Player.Position.ToIndex(_world.CurrentMap._width)].fovMap.CurrentFOV)
            {
                if (!_world.CurrentMap._tiles[pos.ToIndex(_world.CurrentMap._width)].IsExplored)
                {
                    _world.CurrentMap._tiles[pos.ToIndex(_world.CurrentMap._width)].IsExplored = true;
                    _world.CurrentMap._tiles[pos.ToIndex(_world.CurrentMap._width)].IsVisible = true;
                }

                // System.Console.WriteLine(pos + "   p: " + _world.player.Position);
                _world.CurrentMap._tiles[pos.ToIndex(_world.CurrentMap._width)].Foreground.A = 255;

                if (_world.CurrentMap.Entities.Contains(pos))
                    _world.CurrentMap.GetFirstEntityAt<Entity>(pos).Animation.IsVisible = true;

            }

            _mapConsole.IsDirty = true;
        }
        private void ChangeState(States state)
        {
            _gameState = state;
        }
        private void StartGame()
        {
            _menuWindow.Hide();
            _mapWindow.Show();

            _world.CreateWorld(_mapWindow.Width, _mapWindow.Height);
            _world.CurrentMap = _world.OverworldMap;
            LoadMapToConsole(_world.OverworldMap);
            System.Console.WriteLine(_world.OverworldMap);

            _gameState = States.Main;
            DisplayFOV();
            _mainLoop.Init(_world.OverworldMap);
            _mainLoop.Loop();
            //SyncMapEntities(_world.OverworldMap);
        }

        private void LoadMapToConsole(Map map)
        {
            // Now Sync all of the map's entities
            _mapConsole.SetSurface(map._tiles, _mapWindow.Width, _mapWindow.Height);
            SyncMapEntities(map);
        }



        private void SyncMapEntities(Map map)
        {
            // remove all Entities from the console first
            _mapConsole.Children.Clear();
            // Now pull all of the entity sprites into the MapConsole in bulk
            foreach (Entity entity in map.Entities.Items)
            {
                _mapConsole.Children.Add(entity);
            }
            // Subscribe to the Entities ItemAdded listener, so we can keep our MapConsole entities in sync
            map.Entities.ItemAdded += OnMapEntityAdded;

            // Subscribe to the Entities ItemRemoved listener, so we can keep our MapConsole entities in sync
            map.Entities.ItemRemoved += OnMapEntityRemoved;
        }

        // Remove an Entity from the MapConsole every time the Map's Entity collection changes 
        private void OnMapEntityRemoved(object sender, ItemEventArgs<Entity> args)
        {
            _mapConsole.Children.Remove(args.Item);
        }

        // Add an Entity to the MapConsole every time the Map's Entity collection changes
        private void OnMapEntityAdded(object sender, ItemEventArgs<Entity> args)
        {
            System.Console.WriteLine("lolu1");
            _mapConsole.Children.Add(args.Item);
        }

        // ============INPUT===================================

        private void StartMenuKeyboard()
        {
            if (Global.KeyboardState.IsKeyPressed(Keys.Enter))
            {
                StartGame();
            }
        }
        private void WorldMapKeyboard()
        {
            if (Global.KeyboardState.IsKeyReleased(Keys.Enter))
            {
                _gameState = States.Main;
            }
            if (Keyboard.GetState().GetPressedKeys().Length > 0)
            {
                if (Global.KeyboardState.IsKeyPressed(Keys.Up))
                    Command.Move(_world.Player, Tools.Dirs.N);
                if (Global.KeyboardState.IsKeyPressed(Keys.Down))
                    Command.Move(_world.Player, Tools.Dirs.S);
                if (Global.KeyboardState.IsKeyPressed(Keys.Right))
                    Command.Move(_world.Player, Tools.Dirs.E);
                if (Global.KeyboardState.IsKeyPressed(Keys.Left))
                    Command.Move(_world.Player, Tools.Dirs.W);
                DisplayFOV();
                _gameState = States.Main;

            }
        }
        private void CheckMouse()
        {

            if (Global.MouseState.LeftClicked)
                System.Console.WriteLine(_world.OverworldMap.GetTileAt(Global.MouseState.ScreenPosition.PixelLocationToConsole(_mapWindow.Width, _mapWindow.Height)).Name);
        }
    }

    //=================HELPERS=================================================================
    public static class Fonts
    {
        public static FontMaster font1 = Global.LoadFont(@"../../res/fonts/bisasam.font");
        public static FontMaster font2 = Global.LoadFont(@"../../res/fonts/lord.font");
        public static FontMaster font3 = Global.LoadFont(@"../../res/fonts/Anikki-square.font");
        public static Font halfSizeFont = font1.GetFont(Font.FontSizes.Half);
        public static Font normalSizeFont = font2.GetFont(Font.FontSizes.One);
        public static Font quarterSizeFont = font3.GetFont(Font.FontSizes.Quarter);
        public static Font squareHalfFont = font3.GetFont(Font.FontSizes.Half);
    }
    public static class ColorScheme
    {
        public static Color First = Color.Black;
        public static Color Second = new Color(138,247,228);// Color.AntiqueWhite;  //1,255,255
        public static Color Three = new Color(157, 114, 255);//Color.ForestGreen;
        public static Color Four = new Color(255, 179, 253);//Color.LightGoldenrodYellow);Color.LightGoldenrodYellow;
        public static Color Five = new Color(1,255,195);//Color.Aquamarine;
    }
    public enum States
    {
        StartMenu,
        Input,
        Main,
        Animation
    }
}

