using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;

namespace LazyBrushSharp
{
    public class GameLazyBrush : Microsoft.Xna.Framework.Game
    {
        LazyBrush lz;

        // 入力・出力画像
        const string srcImageName = "BlackWell";
        Texture2D srcImage, brushedImage, dstImage;

        // 入力
        KeyboardState prev_ks;
        MouseState prev_ms;

        // ストロークのパス
        List<Vector2> path = new List<Vector2>();

        // カラーピッカー
        System.Windows.Forms.ColorDialog colorDialog = new System.Windows.Forms.ColorDialog();
        Rectangle colorPickerRect = new Rectangle(0, 0, 50, 50);
        Texture2D colorPickerImage;

        // 描画
        const float offsetSrc = -0.7f;
        const float offsetDst = 0.7f;
        const float halfW = 0.5f;
        float halfH;
        readonly Vector3 camPos = new Vector3(0, 0, 2);
        GraphicsDeviceManager graphics;
        SpriteBatch spriteBatch = null;
        SpriteFont font;
        BasicEffect basicEffect = null;
        DynamicVertexBuffer sourceVB = null;
        string debugText = "";

        //--------------------------------------------------

        public GameLazyBrush()
        {
            graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            graphics.PreferredBackBufferWidth = 1280;
            graphics.PreferredBackBufferHeight = 720;

            IsMouseVisible = true;
        }

        protected override void Initialize()
        {
            prev_ks = Keyboard.GetState();
            prev_ms = Mouse.GetState();

            base.Initialize();
        }

        protected override void LoadContent()
        {
            this.spriteBatch = new SpriteBatch(this.GraphicsDevice);

            // フォント
            font = Content.Load<SpriteFont>("Arial");

            // 画像読み込み
            srcImage = Content.Load<Texture2D>(srcImageName);
            brushedImage = new Texture2D(GraphicsDevice, srcImage.Width, srcImage.Height, false, srcImage.Format);// Content.Load<Texture2D>(inputImgName);
            dstImage = new Texture2D(GraphicsDevice, srcImage.Width, srcImage.Height, false, srcImage.Format);// Content.Load<Texture2D>(inputImgName);
            colorPickerImage = Content.Load<Texture2D>("ColorPicker");
            
            // エフェクト
            this.basicEffect = new BasicEffect(this.GraphicsDevice);
            this.basicEffect.TextureEnabled = true;
            this.basicEffect.VertexColorEnabled = true;
            this.basicEffect.View = Matrix.CreateLookAt(
                    camPos,
                    Vector3.Zero,
                    Vector3.Up
                );
            this.basicEffect.Projection = Matrix.CreatePerspectiveFieldOfView(
                    MathHelper.ToRadians(45.0f),
                    (float)this.GraphicsDevice.Viewport.Width /
                        (float)this.GraphicsDevice.Viewport.Height,
                    1.0f,
                    100.0f
                );
            basicEffect.Texture = srcImage;
            basicEffect.TextureEnabled = true;

            // 頂点バッファ
            halfH = halfW * srcImage.Height / srcImage.Width;
            var vertexData =
                new[] {
                    // ストロークを描く画像
                    new VertexPositionColorTexture(
                        new Vector3(offsetSrc-halfW, halfH, 0), Color.White, new Vector2(0, 0)),
                    new VertexPositionColorTexture(
                        new Vector3(offsetSrc+halfW, halfH, 0), Color.White, new Vector2(1, 0)),
                    new VertexPositionColorTexture(
                        new Vector3(offsetSrc-halfW, -halfH, 0), Color.White, new Vector2(0, 1)),
                    new VertexPositionColorTexture(
                        new Vector3(offsetSrc+halfW, -halfH, 0), Color.White, new Vector2(1, 1)),
                
                    // 塗りつぶし後の画像
                    new VertexPositionColorTexture(
                        new Vector3(offsetDst-halfW, halfH, 0), Color.White, new Vector2(0, 0)),
                    new VertexPositionColorTexture(
                        new Vector3(offsetDst+halfW, halfH, 0), Color.White, new Vector2(1, 0)),
                    new VertexPositionColorTexture(
                        new Vector3(offsetDst-halfW, -halfH, 0), Color.White, new Vector2(0, 1)),
                    new VertexPositionColorTexture(
                        new Vector3(offsetDst+halfW, -halfH, 0), Color.White, new Vector2(1, 1))
                };
            sourceVB = new DynamicVertexBuffer(this.GraphicsDevice, VertexPositionColorTexture.VertexDeclaration, vertexData.Length, BufferUsage.None);
            sourceVB.SetData(vertexData, 0, vertexData.Length, SetDataOptions.Discard);

            // LazyBrush
            lz = new LazyBrush(srcImage);
            lz.UpdateFilledTexture(dstImage);
            lz.UpdateBrushedTexture(brushedImage);
        }

        protected override void Update(GameTime gameTime)
        {
            // 入力取得
            KeyboardState ks = Keyboard.GetState();
            MouseState ms = Mouse.GetState();

            // Escで終わる
            if (ks.IsKeyDown(Keys.Escape)) Exit();

            // Enterで塗りつぶし
            if (prev_ks.IsKeyUp(Keys.Enter) && ks.IsKeyDown(Keys.Enter))
            {
                lz.UpdateFilledTexture(dstImage);
            }

            // マウスの左ドラッグでストロークを書く
            if (prev_ms.LeftButton == ButtonState.Released && ms.LeftButton == ButtonState.Pressed)
            {
                path.Clear();
            }
            if (ms.LeftButton == ButtonState.Pressed)
            {
                Vector3 srcTopLeft = GraphicsDevice.Viewport.Project(new Vector3(offsetSrc - halfW, halfH, 0), basicEffect.Projection, basicEffect.View, basicEffect.World);
                Vector3 srcDownRight = GraphicsDevice.Viewport.Project(new Vector3(offsetSrc + halfW, -halfH, 0), basicEffect.Projection, basicEffect.View, basicEffect.World);
                int px = (int)((ms.X - srcTopLeft.X) / (srcDownRight.X - srcTopLeft.X) * srcImage.Width);
                int py = (int)((ms.Y - srcTopLeft.Y) / (srcDownRight.Y - srcTopLeft.Y) * srcImage.Height);
                path.Add(new Vector2(px, py));
                lz.UpdateBrushedTexture(brushedImage, path.ToArray());
            }
            if (prev_ms.LeftButton == ButtonState.Pressed && ms.LeftButton == ButtonState.Released)
            {
                if (path.Count >= 1)
                {
                    lz.AddStroke(path.ToArray());
                    lz.UpdateBrushedTexture(brushedImage);
                }
                path.Clear();
            }

            // Ctr + Z で戻る。Ctrl + Shift + Zで進む。
            if (prev_ks.IsKeyUp(Keys.Z) && ks.IsKeyDown(Keys.Z))
            {
                if (ks.IsKeyDown(Keys.LeftControl) || ks.IsKeyDown(Keys.RightControl))
                {
                    if (ks.IsKeyDown(Keys.LeftShift) || ks.IsKeyDown(Keys.RightShift))
                    {
                        lz.RestoreStroke();
                    }
                    else
                    {
                        lz.PopStroke();
                    }
                    lz.UpdateBrushedTexture(brushedImage);
                }
            }
            
            
            // ブラシの色。画面左上の画像クリックしてカラーピッカーを開く
            if (prev_ms.LeftButton == ButtonState.Released && ms.LeftButton == ButtonState.Pressed)
            {
                if (colorPickerRect.Contains(ms.X, ms.Y))
                {
                    if (colorDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        lz.CurrentFillColor = new Color(colorDialog.Color.R, colorDialog.Color.G, colorDialog.Color.B);
                    }
                }
            }

            // ブラシのサイズ
            if (prev_ks.IsKeyUp(Keys.Up) && ks.IsKeyDown(Keys.Up))
            {
                lz.CurrentRadius++;
            }
            if (prev_ks.IsKeyUp(Keys.Down) && ks.IsKeyDown(Keys.Down))
            {
                lz.CurrentRadius = Math.Max(0, lz.CurrentRadius - 1);
            }

            // 入力更新
            prev_ks = ks;
            prev_ms = ms;

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.LightGray);

            Window.Title = string.Format("色 {0}, ブラシの大きさ: {1} pixel (上下キー), FPS = {2: 00.00}",
                lz.CurrentFillColor.ToString(),
                lz.CurrentRadius,
                1000 / gameTime.ElapsedGameTime.TotalMilliseconds);

            this.GraphicsDevice.SetVertexBuffer(this.sourceVB);

            // インタラクション用（ストロークを描く）画像
//          basicEffect.Texture = src;
            basicEffect.Texture = brushedImage;
            foreach (EffectPass pass in this.basicEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                this.GraphicsDevice.DrawPrimitives(PrimitiveType.TriangleStrip, 0, 2);
            }

            // 塗りつぶし後の画像
            basicEffect.Texture = dstImage;
            foreach (EffectPass pass in this.basicEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                this.GraphicsDevice.DrawPrimitives(PrimitiveType.TriangleStrip, 4, 2);
            }

            spriteBatch.Begin();
            spriteBatch.DrawString(font, debugText, new Vector2(150, 10), Color.Red);
            spriteBatch.Draw(colorPickerImage, colorPickerRect, Color.White);
            spriteBatch.End();

            base.Draw(gameTime);
        }
    }
}