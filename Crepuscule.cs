// Crépuscule — programme l'extinction du PC, façon coucher de soleil.
// Compilation : build.ps1 (utilise le compilateur C# fourni avec Windows, rien à installer).

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
using Dessin = System.Drawing;

namespace Crepuscule
{
    enum TypeAction { Eteindre, Redemarrer, Veille }

    static class Programme
    {
        [STAThread]
        static void Main(string[] args)
        {
            // Utilisé par build.ps1 pour générer l'icône de l'exécutable.
            if (args.Length == 2 && args[0] == "--icone") { Icone.EcrireIco(args[1]); return; }

            // Une seule instance : la seconde se contente de réveiller la première.
            bool premier;
            var mutex = new Mutex(true, "Local\\Crepuscule.Instance", out premier);
            var signal = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\Crepuscule.Montrer");
            if (!premier) { signal.Set(); return; }

            var app = new Application();
            var fenetre = new FenetreCrepuscule();
            var ecoute = new Thread(() =>
            {
                while (true)
                {
                    signal.WaitOne();
                    fenetre.Dispatcher.BeginInvoke(new Action(fenetre.Montrer));
                }
            });
            ecoute.IsBackground = true;
            ecoute.Start();
            app.Run(fenetre);
            GC.KeepAlive(mutex);
        }
    }

    // ───────────────────────── Icône (soleil couchant sur la mer) ─────────────────────────
    static class Icone
    {
        static Dessin.Color C(int rgb, int a = 255)
        {
            return Dessin.Color.FromArgb(a, (rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255);
        }

        public static Dessin.Bitmap Dessiner(int t)
        {
            var bmp = new Dessin.Bitmap(t, t, Dessin.Imaging.PixelFormat.Format32bppArgb);
            using (var g = Dessin.Graphics.FromImage(bmp))
            {
                g.SmoothingMode = Dessin.Drawing2D.SmoothingMode.AntiAlias;
                g.PixelOffsetMode = Dessin.Drawing2D.PixelOffsetMode.HighQuality;
                g.Clear(Dessin.Color.Transparent);

                float m = Math.Max(0.5f, t * 0.03f);
                var cadre = new Dessin.RectangleF(m, m, t - 2 * m, t - 2 * m);
                using (var forme = Arrondi(cadre, t * 0.24f))
                {
                    var zone = new Dessin.RectangleF(cadre.X, cadre.Y - 1, cadre.Width, cadre.Height + 2);
                    using (var ciel = new Dessin.Drawing2D.LinearGradientBrush(zone, C(0x1B1F4B), C(0xF7A35C), 90f))
                    {
                        var melange = new Dessin.Drawing2D.ColorBlend();
                        melange.Colors = new[] { C(0x1B1F4B), C(0x8E4A7A), C(0xF7A35C) };
                        melange.Positions = new[] { 0f, 0.5f, 1f };
                        ciel.InterpolationColors = melange;
                        g.FillPath(ciel, forme);
                    }
                    g.SetClip(forme);

                    float horizon = cadre.Top + cadre.Height * 0.64f;
                    float r = cadre.Width * 0.28f;
                    float cx = cadre.Left + cadre.Width / 2f;
                    var disque = new Dessin.RectangleF(cx - r, horizon - r, 2 * r, 2 * r);
                    var haloSoleil = new Dessin.RectangleF(disque.X, disque.Y - 1, disque.Width, r + 2);
                    using (var soleil = new Dessin.Drawing2D.LinearGradientBrush(haloSoleil, C(0xFFF4C2), C(0xFFA03C), 90f))
                        g.FillEllipse(soleil, disque);

                    using (var mer = new Dessin.SolidBrush(C(0x221A42)))
                        g.FillRectangle(mer, cadre.Left, horizon, cadre.Width, cadre.Bottom - horizon + 1);

                    if (t >= 24)
                    {
                        using (var reflet = new Dessin.Pen(C(0xFFB45A, 220), Math.Max(1f, t * 0.04f)))
                        {
                            reflet.StartCap = reflet.EndCap = Dessin.Drawing2D.LineCap.Round;
                            for (int i = 0; i < 3; i++)
                            {
                                float l = r * (1.5f - i * 0.45f);
                                float y = horizon + cadre.Height * (0.08f + i * 0.08f);
                                g.DrawLine(reflet, cx - l / 2, y, cx + l / 2, y);
                            }
                        }
                    }
                    g.ResetClip();
                }
            }
            return bmp;
        }

        static Dessin.Drawing2D.GraphicsPath Arrondi(Dessin.RectangleF r, float rayon)
        {
            float d = rayon * 2;
            var p = new Dessin.Drawing2D.GraphicsPath();
            p.AddArc(r.Left, r.Top, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Top, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        // Fichier .ico multi-tailles, images 32 bits (BMP + alpha).
        public static void EcrireIco(string chemin)
        {
            int[] tailles = { 16, 24, 32, 48, 64, 256 };
            var images = new List<byte[]>();
            foreach (int t in tailles)
                using (var bmp = Dessiner(t)) images.Add(Dib(bmp));

            using (var flux = new System.IO.FileStream(chemin, System.IO.FileMode.Create))
            using (var w = new System.IO.BinaryWriter(flux))
            {
                w.Write((ushort)0); w.Write((ushort)1); w.Write((ushort)tailles.Length);
                int position = 6 + 16 * tailles.Length;
                for (int i = 0; i < tailles.Length; i++)
                {
                    byte cote = (byte)(tailles[i] >= 256 ? 0 : tailles[i]);
                    w.Write(cote); w.Write(cote); w.Write((byte)0); w.Write((byte)0);
                    w.Write((ushort)1); w.Write((ushort)32);
                    w.Write((uint)images[i].Length); w.Write((uint)position);
                    position += images[i].Length;
                }
                foreach (var image in images) w.Write(image);
            }
        }

        static byte[] Dib(Dessin.Bitmap bmp)
        {
            int t = bmp.Width;
            int masque = ((t + 31) / 32) * 4 * t;
            using (var flux = new System.IO.MemoryStream())
            using (var w = new System.IO.BinaryWriter(flux))
            {
                w.Write(40); w.Write(t); w.Write(t * 2); w.Write((short)1); w.Write((short)32);
                w.Write(0); w.Write(t * t * 4 + masque); w.Write(0); w.Write(0); w.Write(0); w.Write(0);
                for (int y = t - 1; y >= 0; y--)
                    for (int x = 0; x < t; x++)
                    {
                        var c = bmp.GetPixel(x, y);
                        w.Write(c.B); w.Write(c.G); w.Write(c.R); w.Write(c.A);
                    }
                w.Write(new byte[masque]);
                w.Flush();
                return flux.ToArray();
            }
        }

        public static Dessin.Icon PourBarre()
        {
            using (var bmp = Dessiner(Forms.SystemInformation.SmallIconSize.Width))
                return Dessin.Icon.FromHandle(bmp.GetHicon());
        }

        public static ImageSource PourFenetre()
        {
            using (var bmp = Dessiner(64))
            {
                var flux = new System.IO.MemoryStream();
                bmp.Save(flux, Dessin.Imaging.ImageFormat.Png);
                flux.Position = 0;
                var image = new BitmapImage();
                image.BeginInit();
                image.StreamSource = flux;
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.EndInit();
                image.Freeze();
                return image;
            }
        }
    }

    // ───────────────────────── Programmation côté Windows + mémoire ─────────────────────────
    static class Planificateur
    {
        [DllImport("powrprof.dll")]
        static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);

        [DllImport("kernel32.dll")]
        static extern ulong GetTickCount64();

        static readonly string dossier = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Crepuscule");
        static string FichierEtat { get { return System.IO.Path.Combine(dossier, "etat.txt"); } }
        static string FichierPrefs { get { return System.IO.Path.Combine(dossier, "preferences.txt"); } }

        // Extinction / redémarrage confiés à Windows : ils ont lieu même si l'app est fermée.
        // La veille, elle, est déclenchée par l'app à l'échéance.
        // Renvoie 0 si tout s'est bien passé, sinon le code d'erreur de shutdown.exe.
        public static int Programmer(TypeAction action, int secondes, string commentaire)
        {
            if (action == TypeAction.Veille) return 0;
            Shutdown("/a");
            string commande = action == TypeAction.Eteindre ? "/s" : "/r";
            return Shutdown(commande + " /t " + secondes + " /c \"" + commentaire + "\"");
        }

        public static void Annuler(TypeAction action)
        {
            if (action != TypeAction.Veille) Shutdown("/a");
            EffacerEtat();
        }

        public static void MettreEnVeille()
        {
            SetSuspendState(false, false, false);
        }

        static int Shutdown(string arguments)
        {
            try
            {
                var info = new ProcessStartInfo(System.IO.Path.Combine(Environment.SystemDirectory, "shutdown.exe"), arguments)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (var p = Process.Start(info))
                {
                    p.WaitForExit(10000);
                    return p.HasExited ? p.ExitCode : -1;
                }
            }
            catch { return -1; }
        }

        // Heure de démarrage de Windows : sert à détecter qu'un redémarrage a eu lieu entre-temps.
        static DateTime Demarrage()
        {
            return DateTime.UtcNow.AddMilliseconds(-(double)GetTickCount64());
        }

        public static void SauverEtat(TypeAction action, DateTime cibleUtc)
        {
            Ecrire(FichierEtat, action + "\n"
                + cibleUtc.ToString("o", CultureInfo.InvariantCulture) + "\n"
                + Demarrage().ToString("o", CultureInfo.InvariantCulture));
        }

        public static bool LireEtat(out TypeAction action, out DateTime cibleUtc)
        {
            action = TypeAction.Eteindre;
            cibleUtc = DateTime.MinValue;
            try
            {
                if (!System.IO.File.Exists(FichierEtat)) return false;
                var lignes = System.IO.File.ReadAllLines(FichierEtat);
                action = (TypeAction)Enum.Parse(typeof(TypeAction), lignes[0]);
                cibleUtc = DateTime.Parse(lignes[1], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                var demarrage = DateTime.Parse(lignes[2], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                bool redemarre = Math.Abs((Demarrage() - demarrage).TotalMinutes) > 5;
                if (redemarre || cibleUtc <= DateTime.UtcNow) { EffacerEtat(); return false; }
                return true;
            }
            catch { EffacerEtat(); return false; }
        }

        public static void EffacerEtat()
        {
            try { System.IO.File.Delete(FichierEtat); } catch { }
        }

        public static void SauverPrefs(double minutes, TypeAction action, bool anglais)
        {
            Ecrire(FichierPrefs, ((int)Math.Round(minutes)).ToString(CultureInfo.InvariantCulture) + "\n"
                + action + "\n" + (anglais ? "en" : "fr"));
        }

        public static void LirePrefs(ref double minutes, ref TypeAction action, ref bool anglais)
        {
            try
            {
                var lignes = System.IO.File.ReadAllLines(FichierPrefs);
                minutes = int.Parse(lignes[0], CultureInfo.InvariantCulture);
                action = (TypeAction)Enum.Parse(typeof(TypeAction), lignes[1]);
                if (lignes.Length > 2) anglais = lignes[2] == "en";
            }
            catch { }
        }

        static void Ecrire(string fichier, string contenu)
        {
            try
            {
                System.IO.Directory.CreateDirectory(dossier);
                System.IO.File.WriteAllText(fichier, contenu);
            }
            catch { }
        }
    }

    // ───────────────────────── La fenêtre ─────────────────────────
    class FenetreCrepuscule : Window
    {
        const double C = 145, R = 118, MAX_CADRAN = 240;   // cadran : centre, rayon, 4 h pour un tour
        const double LARGEUR = 420, HAUTEUR = 660, MARGE = 16;

        static readonly FontFamily Police = new FontFamily("Segoe UI Variable Display, Segoe UI");
        static readonly FontFamily Icones = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets");

        // Ciel : jour → crépuscule → nuit
        static readonly Color[] Haut = { Rgb(0x264682), Rgb(0x28245C), Rgb(0x06091A) };
        static readonly Color[] Milieu = { Rgb(0x6A87BE), Rgb(0x8E4A7A), Rgb(0x121636) };
        static readonly Color[] Bas = { Rgb(0xF7B76E), Rgb(0xF47A4C), Rgb(0x2C1E4E) };

        TypeAction action = TypeAction.Eteindre;
        bool anglais, modeHeure, enCours, averti, glisse, fermetureReelle, bulleMontree;
        double minutesChoisies = 60;
        int heureCible = 23, minuteCible;
        DateTime cibleUtc, messageJusqua, dernierTexteBarre;
        double valeurAffichee, nuit = -1;
        readonly Stopwatch chrono = Stopwatch.StartNew();

        GradientStop stopHaut, stopMilieu, stopBas;
        readonly List<Ellipse> etoiles = new List<Ellipse>();
        readonly List<double> phases = new List<double>();
        Path lune, arc;
        ArcSegment segmentArc;
        Canvas cadran;
        Ellipse anneau, soleil;
        ScaleTransform echelleSoleil;
        TextBlock texteHaut, texteDuree, texteBas, texteMessage, textePoeme, texteNote, texteHH, texteMM;
        FrameworkElement panneauReglage, panneauEnCours, zonePuces, zoneHeure, basEnCours;
        Border segDuree, segHeure, boutonLancer;
        readonly List<KeyValuePair<Border, double>> puces = new List<KeyValuePair<Border, double>>();
        readonly Border[] boutonsAction = new Border[3];
        Border langueFr, langueEn;
        // Chaque texte traduisible s'enregistre ici : changer de langue les rejoue toutes.
        readonly List<Action> traductions = new List<Action>();
        Forms.NotifyIcon icone;
        DispatcherTimer horloge;

        public FenetreCrepuscule()
        {
            Title = "Crépuscule";
            Width = LARGEUR + 2 * MARGE;
            Height = HAUTEUR + 2 * MARGE;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            FontFamily = Police;
            Foreground = Brushes.White;
            UseLayoutRounding = true;
            Icon = Icone.PourFenetre();

            // Par défaut, la langue de Windows ; ensuite, le dernier choix.
            anglais = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName != "fr";
            Planificateur.LirePrefs(ref minutesChoisies, ref action, ref anglais);
            minutesChoisies = Math.Max(0, Math.Min(MAX_CADRAN, minutesChoisies));

            Content = Construire();
            CreerIconeBarre();

            // Une extinction était-elle déjà programmée ?
            TypeAction actionEnCours;
            DateTime cible;
            if (Planificateur.LireEtat(out actionEnCours, out cible))
            {
                action = actionEnCours;
                cibleUtc = cible;
                enCours = true;
            }

            valeurAffichee = DureeActuelle();
            MettreAJourControles();

            horloge = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(25) };
            horloge.Tick += (s, e) => Tic();
            horloge.Start();
            IsVisibleChanged += (s, e) => horloge.Interval = TimeSpan.FromMilliseconds(IsVisible ? 25 : 1000);

            MouseLeftButtonDown += (s, e) => { try { DragMove(); } catch { } };
            KeyDown += ToucheClavier;
            Closing += Fermeture;
        }

        // ───────── Construction de l'interface ─────────

        UIElement Construire()
        {
            var racine = new Grid { Margin = new Thickness(MARGE) };
            racine.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(28),
                Background = new SolidColorBrush(Color.FromRgb(10, 8, 30)),
                Effect = new DropShadowEffect { BlurRadius = 16, ShadowDepth = 3, Opacity = 0.45, Color = Colors.Black }
            });

            var ciel = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
            stopHaut = new GradientStop(Haut[0], 0);
            stopMilieu = new GradientStop(Milieu[0], 0.55);
            stopBas = new GradientStop(Bas[0], 1);
            ciel.GradientStops.Add(stopHaut);
            ciel.GradientStops.Add(stopMilieu);
            ciel.GradientStops.Add(stopBas);

            var carte = new Grid
            {
                Background = ciel,
                Clip = new RectangleGeometry(new Rect(0, 0, LARGEUR, HAUTEUR), 28, 28)
            };
            racine.Children.Add(carte);

            carte.Children.Add(ConstruireNuit());
            carte.Children.Add(new Path
            {
                Data = Geometry.Parse("M0,560 C90,520 150,548 220,530 C300,510 350,534 420,516 L420,660 L0,660 Z"),
                Fill = new SolidColorBrush(Color.FromArgb(60, 8, 6, 28)),
                IsHitTestVisible = false
            });
            carte.Children.Add(new Path
            {
                Data = Geometry.Parse("M0,604 C70,586 130,574 200,592 C270,610 340,582 420,596 L420,660 L0,660 Z"),
                Fill = new SolidColorBrush(Color.FromArgb(110, 8, 6, 28)),
                IsHitTestVisible = false
            });

            var contenu = new Grid();
            foreach (var h in new[] { GridLength.Auto, GridLength.Auto, GridLength.Auto, new GridLength(1, GridUnitType.Star), GridLength.Auto })
                contenu.RowDefinitions.Add(new RowDefinition { Height = h });

            Ligne(contenu, ConstruireBarreTitre(), 0);
            Ligne(contenu, ConstruireCadran(), 1);

            texteMessage = Texte("", 13, FontWeights.Normal, 0.9);
            texteMessage.TextAlignment = TextAlignment.Center;
            texteMessage.TextWrapping = TextWrapping.Wrap;
            texteMessage.Margin = new Thickness(30, 4, 30, 0);
            texteMessage.MinHeight = 18;
            Ligne(contenu, texteMessage, 2);

            panneauReglage = ConstruireReglages();
            panneauEnCours = ConstruirePanneauEnCours();
            Ligne(contenu, panneauReglage, 3);
            Ligne(contenu, panneauEnCours, 3);

            var bas = new Grid { Margin = new Thickness(0, 0, 0, 28) };
            boutonLancer = ConstruireBoutonLancer();
            basEnCours = ConstruireBoutonsEnCours();
            bas.Children.Add(boutonLancer);
            bas.Children.Add(basEnCours);
            Ligne(contenu, bas, 4);

            carte.Children.Add(contenu);
            return racine;
        }

        UIElement ConstruireNuit()
        {
            var couche = new Canvas { IsHitTestVisible = false };
            var hasard = new Random(7);
            for (int i = 0; i < 46; i++)
            {
                double d = 1 + hasard.NextDouble() * 1.8;
                var etoile = new Ellipse { Width = d, Height = d, Fill = Brushes.White, Opacity = 0 };
                Canvas.SetLeft(etoile, 14 + hasard.NextDouble() * (LARGEUR - 28));
                Canvas.SetTop(etoile, 12 + hasard.NextDouble() * 400);
                couche.Children.Add(etoile);
                etoiles.Add(etoile);
                phases.Add(hasard.NextDouble() * Math.PI * 2);
            }
            var croissant = new CombinedGeometry(GeometryCombineMode.Exclude,
                new EllipseGeometry(new Point(0, 0), 15, 15),
                new EllipseGeometry(new Point(7, -5), 13, 13));
            lune = new Path
            {
                Data = croissant,
                Fill = new SolidColorBrush(Rgb(0xFFF1D0)),
                Opacity = 0,
                Effect = new DropShadowEffect { Color = Rgb(0xFFF1D0), BlurRadius = 16, ShadowDepth = 0, Opacity = 0.6 }
            };
            Canvas.SetLeft(lune, 372);
            Canvas.SetTop(lune, 88);
            couche.Children.Add(lune);
            return couche;
        }

        UIElement ConstruireBarreTitre()
        {
            var barre = new Grid { Margin = new Thickness(24, 16, 14, 0) };
            var titre = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            titre.Children.Add(new Ellipse
            {
                Width = 9, Height = 9,
                Fill = new SolidColorBrush(Rgb(0xFFB547)),
                Margin = new Thickness(0, 1, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
            titre.Children.Add(Texte("Crépuscule", 14, FontWeights.SemiBold, 0.92));
            barre.Children.Add(titre);

            var boutons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };

            // Sélecteur de langue FR | EN
            var langues = new StackPanel { Orientation = Orientation.Horizontal };
            langueFr = PuceLangue("FR", false);
            langueEn = PuceLangue("EN", true);
            langues.Children.Add(langueFr);
            langues.Children.Add(langueEn);
            var selecteurLangue = new Border
            {
                Height = 30, Padding = new Thickness(3), Margin = new Thickness(0, 0, 6, 0),
                CornerRadius = new CornerRadius(15), Background = Verre(0.08),
                VerticalAlignment = VerticalAlignment.Center, Child = langues
            };
            Traduire(() => selecteurLangue.ToolTip = L("Langue de l'interface", "Interface language"));
            boutons.Children.Add(selecteurLangue);
            boutons.Children.Add(BoutonRond("", "Ranger près de l'horloge", "Hide next to the clock", Reduire));
            boutons.Children.Add(BoutonRond("", "Fermer", "Close", Close));
            barre.Children.Add(boutons);
            return barre;
        }

        UIElement ConstruireCadran()
        {
            var zone = new Grid { Width = 2 * C, Height = 2 * C, Margin = new Thickness(0, 4, 0, 0) };
            cadran = new Canvas { Background = Brushes.Transparent, Cursor = Cursors.Hand };

            var halo = new Ellipse
            {
                Width = 2 * R + 70, Height = 2 * R + 70,
                Fill = new RadialGradientBrush(Color.FromArgb(46, 255, 214, 150), Color.FromArgb(0, 255, 214, 150)),
                IsHitTestVisible = false
            };
            Placer(halo, C - R - 35, C - R - 35);

            // La forme Ellipse dessine son trait à l'intérieur : on l'agrandit pour centrer le trait sur R.
            anneau = new Ellipse { Width = 2 * R + 14, Height = 2 * R + 14, Stroke = Verre(0.16), StrokeThickness = 14, IsHitTestVisible = false };
            Placer(anneau, C - R - 7, C - R - 7);

            for (int i = 0; i < 48; i++)
            {
                double a = i / 48.0 * 2 * Math.PI;
                bool heure = i % 12 == 0;
                double r1 = heure ? R - 28 : R - 22, r2 = R - 15;
                cadran.Children.Add(new Line
                {
                    X1 = C + r1 * Math.Sin(a), Y1 = C - r1 * Math.Cos(a),
                    X2 = C + r2 * Math.Sin(a), Y2 = C - r2 * Math.Cos(a),
                    Stroke = Verre(heure ? 0.55 : 0.22),
                    StrokeThickness = heure ? 2 : 1.2,
                    StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round,
                    IsHitTestVisible = false
                });
            }
            for (int k = 1; k <= 3; k++)
            {
                double a = k / 4.0 * 2 * Math.PI, r = R - 42;
                var repere = Texte(k + " h", 10.5, FontWeights.Normal, 0.45);
                repere.Width = 32;
                repere.TextAlignment = TextAlignment.Center;
                repere.IsHitTestVisible = false;
                Placer(repere, C + r * Math.Sin(a) - 16, C - r * Math.Cos(a) - 8);
            }

            segmentArc = new ArcSegment { Size = new Size(R, R), SweepDirection = SweepDirection.Clockwise };
            var figure = new PathFigure { StartPoint = new Point(C, C - R) };
            figure.Segments.Add(segmentArc);
            var geometrie = new PathGeometry();
            geometrie.Figures.Add(figure);
            var degrade = new LinearGradientBrush
            {
                MappingMode = BrushMappingMode.Absolute,
                StartPoint = new Point(C + R, C - R),
                EndPoint = new Point(C - R, C + R)
            };
            degrade.GradientStops.Add(new GradientStop(Rgb(0xFFE7A0), 0));
            degrade.GradientStops.Add(new GradientStop(Rgb(0xFFA05A), 0.5));
            degrade.GradientStops.Add(new GradientStop(Rgb(0xFF5C8A), 1));
            arc = new Path
            {
                Data = geometrie, Stroke = degrade, StrokeThickness = 14,
                StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round,
                IsHitTestVisible = false
            };
            cadran.Children.Add(arc);

            var lumiere = new RadialGradientBrush();
            lumiere.GradientStops.Add(new GradientStop(Rgb(0xFFFDF2), 0));
            lumiere.GradientStops.Add(new GradientStop(Rgb(0xFFE08A), 0.55));
            lumiere.GradientStops.Add(new GradientStop(Rgb(0xFFA43C), 1));
            echelleSoleil = new ScaleTransform(1, 1);
            soleil = new Ellipse
            {
                Width = 34, Height = 34, Fill = lumiere,
                Stroke = new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)), StrokeThickness = 1.5,
                Effect = new DropShadowEffect { Color = Rgb(0xFFB347), BlurRadius = 26, ShadowDepth = 0, Opacity = 0.95 },
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = echelleSoleil
            };
            cadran.Children.Add(soleil);

            cadran.MouseLeftButtonDown += (s, e) =>
            {
                if (enCours) return;
                glisse = true;
                cadran.CaptureMouse();
                DepuisSouris(e.GetPosition(cadran), false);
                e.Handled = true;
            };
            cadran.MouseMove += (s, e) => { if (glisse) DepuisSouris(e.GetPosition(cadran), true); };
            cadran.MouseLeftButtonUp += (s, e) => { if (glisse) { glisse = false; cadran.ReleaseMouseCapture(); } };
            cadran.LostMouseCapture += (s, e) => glisse = false;
            cadran.MouseWheel += (s, e) => { if (!enCours) Ajuster(e.Delta > 0 ? 1 : -1); e.Handled = true; };
            zone.Children.Add(cadran);

            var centre = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false };
            texteHaut = Texte("", 13, FontWeights.Normal, 0.75);
            texteDuree = Texte("", 44, FontWeights.Light, 1);
            texteDuree.Margin = new Thickness(0, -4, 0, -2);
            texteBas = Texte("", 14, FontWeights.Normal, 0.8);
            foreach (var t in new[] { texteHaut, texteDuree, texteBas })
            {
                t.HorizontalAlignment = HorizontalAlignment.Center;
                centre.Children.Add(t);
            }
            zone.Children.Add(centre);
            return zone;
        }

        FrameworkElement ConstruireReglages()
        {
            var panneau = new StackPanel { Margin = new Thickness(28, 8, 28, 0) };

            // Minuteur | Heure fixe
            var segments = new Grid();
            segments.ColumnDefinitions.Add(new ColumnDefinition());
            segments.ColumnDefinitions.Add(new ColumnDefinition());
            segDuree = Segment("Minuteur", "Timer", () => ChoisirMode(false));
            segHeure = Segment("Heure fixe", "Fixed time", () => ChoisirMode(true));
            Grid.SetColumn(segHeure, 1);
            segments.Children.Add(segDuree);
            segments.Children.Add(segHeure);
            panneau.Children.Add(new Border
            {
                Width = 260, Height = 38, Padding = new Thickness(3),
                CornerRadius = new CornerRadius(19), Background = Verre(0.07),
                HorizontalAlignment = HorizontalAlignment.Center,
                Child = segments
            });

            var zone = new Grid { Height = 46, Margin = new Thickness(0, 12, 0, 0) };
            var rangee = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            foreach (double minutes in new[] { 15.0, 30, 60, 120 })
            {
                double m = minutes;
                var puce = new Border
                {
                    Height = 36, MinWidth = 66, Padding = new Thickness(14, 0, 14, 0), Margin = new Thickness(4, 0, 4, 0),
                    CornerRadius = new CornerRadius(18), BorderThickness = new Thickness(1),
                    Child = Centre(Texte(m < 60 ? m + " min" : m / 60 + " h", 13.5, FontWeights.Normal, 1))
                };
                Brancher(puce, () => DefinirMinutes(m));
                puces.Add(new KeyValuePair<Border, double>(puce, m));
                rangee.Children.Add(puce);
            }
            zonePuces = rangee;
            zone.Children.Add(rangee);

            var selecteur = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            texteHH = GrandChiffre(60);
            texteMM = GrandChiffre(1);
            selecteur.Children.Add(BoutonRond("", "Une heure plus tôt", "One hour earlier", () => ChangerHeure(-60)));
            selecteur.Children.Add(texteHH);
            selecteur.Children.Add(BoutonRond("", "Une heure plus tard", "One hour later", () => ChangerHeure(60)));
            var deuxPoints = Texte(":", 28, FontWeights.Light, 0.7);
            deuxPoints.Margin = new Thickness(10, -4, 10, 0);
            deuxPoints.VerticalAlignment = VerticalAlignment.Center;
            selecteur.Children.Add(deuxPoints);
            selecteur.Children.Add(BoutonRond("", "5 minutes plus tôt", "5 minutes earlier", () => ChangerHeure(-5)));
            selecteur.Children.Add(texteMM);
            selecteur.Children.Add(BoutonRond("", "5 minutes plus tard", "5 minutes later", () => ChangerHeure(5)));
            zoneHeure = selecteur;
            zone.Children.Add(selecteur);
            panneau.Children.Add(zone);

            var grille = new Grid { Margin = new Thickness(0, 14, 0, 0) };
            string[] glyphes = { "", "", "" };
            string[] noms = { "Éteindre", "Redémarrer", "Veille" };
            string[] names = { "Shut down", "Restart", "Sleep" };
            for (int i = 0; i < 3; i++)
            {
                grille.ColumnDefinitions.Add(new ColumnDefinition());
                var pile = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                var glyphe = Texte(glyphes[i], 19, FontWeights.Normal, 1);
                glyphe.FontFamily = Icones;
                glyphe.HorizontalAlignment = HorizontalAlignment.Center;
                var nom = Texte("", 12.5, FontWeights.Normal, 0.9);
                string fr = noms[i], en = names[i];
                Traduire(() => nom.Text = L(fr, en));
                nom.HorizontalAlignment = HorizontalAlignment.Center;
                nom.Margin = new Thickness(0, 6, 0, 0);
                pile.Children.Add(glyphe);
                pile.Children.Add(nom);
                var bouton = new Border
                {
                    Height = 64, CornerRadius = new CornerRadius(18), BorderThickness = new Thickness(1),
                    Margin = new Thickness(i == 0 ? 0 : 5, 0, i == 2 ? 0 : 5, 0),
                    Child = pile
                };
                var type = (TypeAction)i;
                Brancher(bouton, () => ChoisirAction(type));
                Grid.SetColumn(bouton, i);
                grille.Children.Add(bouton);
                boutonsAction[i] = bouton;
            }
            panneau.Children.Add(grille);
            return panneau;
        }

        FrameworkElement ConstruirePanneauEnCours()
        {
            var panneau = new StackPanel { Margin = new Thickness(40, 22, 40, 0) };
            textePoeme = Texte("", 18, FontWeights.Normal, 0.95);
            textePoeme.FontStyle = FontStyles.Italic;
            textePoeme.TextAlignment = TextAlignment.Center;
            textePoeme.TextWrapping = TextWrapping.Wrap;
            texteNote = Texte("", 12.5, FontWeights.Normal, 0.65);
            texteNote.TextAlignment = TextAlignment.Center;
            texteNote.TextWrapping = TextWrapping.Wrap;
            texteNote.Margin = new Thickness(0, 14, 0, 0);
            panneau.Children.Add(textePoeme);
            panneau.Children.Add(texteNote);
            return panneau;
        }

        Border ConstruireBoutonLancer()
        {
            var fond = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
            fond.GradientStops.Add(new GradientStop(Rgb(0xFFC15E), 0));
            fond.GradientStops.Add(new GradientStop(Rgb(0xFF6A5C), 0.55));
            fond.GradientStops.Add(new GradientStop(Rgb(0xE0487F), 1));
            var bouton = new Border
            {
                Width = 300, Height = 54, CornerRadius = new CornerRadius(27), Background = fond,
                HorizontalAlignment = HorizontalAlignment.Center,
                Effect = new DropShadowEffect { Color = Rgb(0xFF7A50), BlurRadius = 22, ShadowDepth = 0, Opacity = 0.55 },
                Child = Etiquette("", "Lancer le crépuscule", "Start the sunset", 16)
            };
            Brancher(bouton, Lancer);
            return bouton;
        }

        FrameworkElement ConstruireBoutonsEnCours()
        {
            var rangee = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            var plus = Pilule("", "+15 min", "+15 min", Verre(0.14), Prolonger);
            var annuler = Pilule("", "Annuler", "Cancel", new SolidColorBrush(Color.FromArgb(90, 255, 90, 110)), Annuler);
            plus.Margin = new Thickness(0, 0, 6, 0);
            annuler.Margin = new Thickness(6, 0, 0, 0);
            rangee.Children.Add(plus);
            rangee.Children.Add(annuler);
            return rangee;
        }

        // ───────── Petites briques visuelles ─────────

        static Color Rgb(int v) { return Color.FromRgb((byte)(v >> 16), (byte)(v >> 8), (byte)v); }

        static SolidColorBrush Verre(double alpha)
        {
            return new SolidColorBrush(Color.FromArgb((byte)(alpha * 255), 255, 255, 255));
        }

        static TextBlock Texte(string texte, double taille, FontWeight graisse, double opacite)
        {
            return new TextBlock { Text = texte, FontSize = taille, FontWeight = graisse, Opacity = opacite, Foreground = Brushes.White };
        }

        static UIElement Centre(FrameworkElement e)
        {
            e.HorizontalAlignment = HorizontalAlignment.Center;
            e.VerticalAlignment = VerticalAlignment.Center;
            return e;
        }

        // Traduction : renvoie le texte dans la langue choisie.
        string L(string fr, string en) { return anglais ? en : fr; }

        // Enregistre un texte traduisible et l'applique tout de suite.
        void Traduire(Action appliquer)
        {
            traductions.Add(appliquer);
            appliquer();
        }

        UIElement Etiquette(string glyphe, string fr, string en, double taille)
        {
            var pile = new StackPanel { Orientation = Orientation.Horizontal };
            var icone = Texte(glyphe, taille, FontWeights.Normal, 1);
            icone.FontFamily = Icones;
            icone.VerticalAlignment = VerticalAlignment.Center;
            icone.Margin = new Thickness(0, 1, 10, 0);
            var libelle = Texte("", taille, FontWeights.SemiBold, 1);
            libelle.VerticalAlignment = VerticalAlignment.Center;
            Traduire(() => libelle.Text = L(fr, en));
            pile.Children.Add(icone);
            pile.Children.Add(libelle);
            return Centre(pile);
        }

        void Placer(UIElement e, double x, double y)
        {
            Canvas.SetLeft(e, x);
            Canvas.SetTop(e, y);
            cadran.Children.Add(e);
        }

        static void Ligne(Grid grille, UIElement e, int ligne)
        {
            Grid.SetRow(e, ligne);
            grille.Children.Add(e);
        }

        Border BoutonRond(string glyphe, string infobulleFr, string infobulleEn, Action clic)
        {
            var texte = Texte(glyphe, 11, FontWeights.Normal, 0.9);
            texte.FontFamily = Icones;
            var bouton = new Border
            {
                Width = 30, Height = 30, CornerRadius = new CornerRadius(15), Background = Verre(0.08),
                Margin = new Thickness(3, 0, 3, 0), VerticalAlignment = VerticalAlignment.Center,
                Child = Centre(texte)
            };
            Traduire(() => bouton.ToolTip = L(infobulleFr, infobulleEn));
            Brancher(bouton, clic);
            return bouton;
        }

        Border Segment(string fr, string en, Action clic)
        {
            var texte = Texte("", 13, FontWeights.Normal, 1);
            Traduire(() => texte.Text = L(fr, en));
            var segment = new Border { CornerRadius = new CornerRadius(16), Child = Centre(texte) };
            Brancher(segment, clic);
            return segment;
        }

        Border PuceLangue(string code, bool versAnglais)
        {
            var puce = new Border
            {
                Width = 30, Height = 24, CornerRadius = new CornerRadius(12),
                Child = Centre(Texte(code, 11, FontWeights.SemiBold, 0.95))
            };
            Brancher(puce, () => ChoisirLangue(versAnglais));
            return puce;
        }

        Border Pilule(string glyphe, string fr, string en, Brush fond, Action clic)
        {
            var pilule = new Border
            {
                Width = 150, Height = 50, CornerRadius = new CornerRadius(25), Background = fond,
                BorderBrush = Verre(0.25), BorderThickness = new Thickness(1),
                Child = Etiquette(glyphe, fr, en, 15)
            };
            Brancher(pilule, clic);
            return pilule;
        }

        TextBlock GrandChiffre(int pasMolette)
        {
            var t = Texte("00", 28, FontWeights.Light, 1);
            t.Width = 48;
            t.TextAlignment = TextAlignment.Center;
            t.VerticalAlignment = VerticalAlignment.Center;
            t.Background = Brushes.Transparent;
            Traduire(() => t.ToolTip = L("Molette pour ajuster", "Scroll to adjust"));
            t.MouseWheel += (s, e) => { ChangerHeure(e.Delta > 0 ? pasMolette : -pasMolette); e.Handled = true; };
            return t;
        }

        // Rend un Border cliquable, avec un léger rebond au survol et à l'appui.
        static void Brancher(Border b, Action clic)
        {
            bool presse = false;
            b.Cursor = Cursors.Hand;
            b.RenderTransformOrigin = new Point(0.5, 0.5);
            b.RenderTransform = new ScaleTransform(1, 1);
            b.MouseEnter += (s, e) => Echelle(b, presse ? 0.96 : 1.04);
            b.MouseLeave += (s, e) => Echelle(b, 1);
            b.MouseLeftButtonDown += (s, e) =>
            {
                presse = true;
                b.CaptureMouse();
                Echelle(b, 0.96);
                e.Handled = true;
            };
            b.MouseLeftButtonUp += (s, e) =>
            {
                if (!presse) return;
                presse = false;
                b.ReleaseMouseCapture();
                var p = e.GetPosition(b);
                bool dedans = p.X >= 0 && p.Y >= 0 && p.X <= b.ActualWidth && p.Y <= b.ActualHeight;
                Echelle(b, dedans ? 1.04 : 1);
                e.Handled = true;
                if (dedans) clic();
            };
        }

        static void Echelle(Border b, double valeur)
        {
            var transformation = (ScaleTransform)b.RenderTransform;
            var animation = new DoubleAnimation(valeur, TimeSpan.FromMilliseconds(140))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            transformation.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
            transformation.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
        }

        static void Choisi(Border b, bool oui)
        {
            b.Background = Verre(oui ? 0.24 : 0.07);
            b.BorderBrush = Verre(oui ? 0.5 : 0.12);
        }

        // ───────── Zone de notification ─────────

        void CreerIconeBarre()
        {
            icone = new Forms.NotifyIcon { Icon = Icone.PourBarre(), Text = "Crépuscule", Visible = true };
            var menu = new Forms.ContextMenuStrip();
            var ouvrir = menu.Items.Add("", null, (s, e) => Montrer());
            var annuler = menu.Items.Add("", null, (s, e) => Annuler());
            menu.Items.Add(new Forms.ToolStripSeparator());
            var quitter = menu.Items.Add("", null, (s, e) => { fermetureReelle = true; Close(); });
            Traduire(() =>
            {
                ouvrir.Text = L("Ouvrir Crépuscule", "Open Crépuscule");
                annuler.Text = L("Annuler la programmation", "Cancel the schedule");
                quitter.Text = L("Quitter", "Quit");
            });
            menu.Opening += (s, e) => annuler.Enabled = enCours;
            icone.ContextMenuStrip = menu;
            icone.MouseClick += (s, e) => { if (e.Button == Forms.MouseButtons.Left) Montrer(); };
        }

        public void Montrer()
        {
            Show();
            if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
            Activate();
            Topmost = true;
            Topmost = false;
            Focus();
        }

        void Reduire()
        {
            Hide();
            if (bulleMontree) return;
            bulleMontree = true;
            icone.ShowBalloonTip(4000,
                L("Crépuscule reste là", "Crépuscule is still here"),
                L("Un clic sur l'icône près de l'horloge pour le rouvrir.", "Click the icon next to the clock to reopen it."),
                Forms.ToolTipIcon.None);
        }

        void Fermeture(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // La veille dépend de l'app : on se range au lieu de fermer (sauf « Quitter »).
            if (!fermetureReelle && enCours && action == TypeAction.Veille)
            {
                e.Cancel = true;
                Reduire();
                return;
            }
            if (enCours && action == TypeAction.Veille) Planificateur.EffacerEtat();
            Planificateur.SauverPrefs(minutesChoisies, action, anglais);
            icone.Visible = false;
            icone.Dispose();
        }

        // ───────── Logique ─────────

        double DureeActuelle()
        {
            if (enCours) return Math.Max(0, (cibleUtc - DateTime.UtcNow).TotalMinutes);
            if (!modeHeure) return minutesChoisies;
            return (CibleLocale() - DateTime.Now).TotalMinutes;
        }

        DateTime CibleLocale()
        {
            if (enCours) return cibleUtc.ToLocalTime();
            var maintenant = DateTime.Now;
            if (!modeHeure) return maintenant.AddMinutes(minutesChoisies);
            var cible = maintenant.Date.AddHours(heureCible).AddMinutes(minuteCible);
            return cible <= maintenant ? cible.AddDays(1) : cible;
        }

        void DepuisSouris(Point p, bool enGlissement)
        {
            double dx = p.X - C, dy = C - p.Y;
            if (!enGlissement && dx * dx + dy * dy < 28 * 28) return;
            double angle = Math.Atan2(dx, dy);
            if (angle < 0) angle += 2 * Math.PI;
            double v = Math.Round(angle / (2 * Math.PI) * MAX_CADRAN / 5) * 5;
            if (enGlissement)
            {
                // Empêche de « sauter » de 4 h à 0 (ou l'inverse) en passant par midi.
                double actuel = Math.Min(DureeActuelle(), MAX_CADRAN);
                if (actuel > MAX_CADRAN * 0.75 && v < MAX_CADRAN * 0.25) v = MAX_CADRAN;
                else if (actuel < MAX_CADRAN * 0.25 && v > MAX_CADRAN * 0.75) v = 0;
            }
            DefinirMinutes(v);
        }

        void DefinirMinutes(double minutes)
        {
            if (enCours) return;
            minutesChoisies = Math.Max(0, Math.Min(MAX_CADRAN, minutes));
            modeHeure = false;
            MettreAJourControles();
        }

        void Ajuster(int delta)
        {
            if (modeHeure) ChangerHeure(delta);
            else DefinirMinutes(minutesChoisies + delta);
        }

        void ChangerHeure(int delta)
        {
            if (enCours) return;
            int total = ((heureCible * 60 + minuteCible + delta) % 1440 + 1440) % 1440;
            heureCible = total / 60;
            minuteCible = total % 60;
            MettreAJourControles();
        }

        void ChoisirMode(bool heure)
        {
            if (enCours || heure == modeHeure) return;
            if (heure)
            {
                // Propose l'heure correspondant à la durée réglée, arrondie aux 5 minutes.
                var cible = DateTime.Now.AddMinutes(minutesChoisies >= 1 ? minutesChoisies : 60);
                int total = ((int)Math.Ceiling((cible.Hour * 60 + cible.Minute + cible.Second / 60.0) / 5) * 5) % 1440;
                heureCible = total / 60;
                minuteCible = total % 60;
            }
            else
            {
                minutesChoisies = Math.Max(0, Math.Min(MAX_CADRAN, Math.Round(DureeActuelle() / 5) * 5));
            }
            modeHeure = heure;
            MettreAJourControles();
        }

        void ChoisirAction(TypeAction choix)
        {
            if (enCours) return;
            action = choix;
            if (choix == TypeAction.Veille)
                Message(L("Pour la veille, garde Crépuscule ouvert (ou rangé près de l'horloge).",
                          "For sleep, keep Crépuscule open (or hidden next to the clock)."), 7);
            else Message("", 0);
            MettreAJourControles();
        }

        void ChoisirLangue(bool versAnglais)
        {
            if (anglais == versAnglais) return;
            anglais = versAnglais;
            foreach (var appliquer in traductions) appliquer();
            Message("", 0);
            MettreAJourControles();
            MettreAJourBarre();
            Planificateur.SauverPrefs(minutesChoisies, action, anglais);
        }

        // Programme auprès de Windows ; affiche l'erreur éventuelle et renvoie false.
        bool Programmer(int secondes)
        {
            int code = Planificateur.Programmer(action, secondes, L("Programmé avec Crépuscule", "Scheduled with Crépuscule"));
            if (code == 0) return true;
            Message(L("Windows a refusé la programmation (code ", "Windows refused the schedule (code ") + code + ").", 10);
            return false;
        }

        void Lancer()
        {
            if (enCours) return;
            double minutes = DureeActuelle();
            if (minutes < 1) { Message(L("Fais d'abord glisser le soleil sur l'anneau.", "Drag the sun around the ring first.")); return; }

            int secondes = (int)Math.Round(minutes * 60);
            if (!Programmer(secondes)) return;

            cibleUtc = DateTime.UtcNow.AddSeconds(secondes);
            enCours = true;
            averti = false;
            Planificateur.SauverEtat(action, cibleUtc);
            Planificateur.SauverPrefs(minutesChoisies, action, anglais);
            Message("", 0);
            MettreAJourControles();
        }

        void Prolonger()
        {
            if (!enCours) return;
            int secondes = (int)Math.Max(60, (cibleUtc - DateTime.UtcNow).TotalSeconds) + 15 * 60;
            if (!Programmer(secondes)) return;
            cibleUtc = DateTime.UtcNow.AddSeconds(secondes);
            averti = false;
            Planificateur.SauverEtat(action, cibleUtc);
            Message(L("Quinze minutes de lumière en plus.", "Fifteen more minutes of daylight."));
        }

        void Annuler()
        {
            if (!enCours) return;
            Planificateur.Annuler(action);
            enCours = false;
            Message(L("Programmation annulée : le soleil reste levé.", "Schedule cancelled: the sun stays up."));
            MettreAJourControles();
        }

        void Avertir()
        {
            Montrer();
            icone.ShowBalloonTip(8000, Majuscule(Nom()) + L(" dans 5 minutes", " in 5 minutes"),
                L("Pense à sauvegarder ton travail. Tu peux encore prolonger ou annuler.",
                  "Remember to save your work. You can still extend or cancel."), Forms.ToolTipIcon.Warning);
        }

        void Terminer()
        {
            enCours = false;
            Planificateur.EffacerEtat();
            Message(L("Bonne nuit.", "Good night."), 30);
            MettreAJourControles();
            if (action == TypeAction.Veille) Planificateur.MettreEnVeille();
        }

        void Message(string texte, double secondes = 6)
        {
            texteMessage.Text = texte;
            messageJusqua = DateTime.UtcNow.AddSeconds(secondes);
        }

        string Nom()
        {
            if (action == TypeAction.Eteindre) return L("extinction", "shutdown");
            if (action == TypeAction.Redemarrer) return L("redémarrage", "restart");
            return L("mise en veille", "sleep");
        }

        static string Majuscule(string s) { return char.ToUpper(s[0]) + s.Substring(1); }

        static string FormatDuree(double minutes)
        {
            int m = (int)minutes;
            if (m < 60) return m + " min";
            return m % 60 == 0 ? m / 60 + " h" : m / 60 + " h " + (m % 60).ToString("00");
        }

        string Poeme(double secondes)
        {
            if (secondes > 7200) return L("Le soleil a encore de beaux restes.", "The sun still has some glow left.");
            if (secondes > 1800) return L("La lumière décline doucement.", "The light is slowly fading.");
            if (secondes > 600) return L("Le ciel se teinte de rose.", "The sky is turning pink.");
            if (secondes > 300) return L("Les premières étoiles s'allument.", "The first stars are coming out.");
            return L("Pense à sauvegarder ton travail.", "Remember to save your work.");
        }

        void MettreAJourControles()
        {
            var reglage = enCours ? Visibility.Collapsed : Visibility.Visible;
            var compteur = enCours ? Visibility.Visible : Visibility.Collapsed;
            panneauReglage.Visibility = reglage;
            boutonLancer.Visibility = reglage;
            panneauEnCours.Visibility = compteur;
            basEnCours.Visibility = compteur;
            cadran.Cursor = enCours ? Cursors.Arrow : Cursors.Hand;

            segDuree.Background = modeHeure ? Brushes.Transparent : Verre(0.26);
            segHeure.Background = modeHeure ? Verre(0.26) : Brushes.Transparent;
            zonePuces.Visibility = modeHeure ? Visibility.Collapsed : Visibility.Visible;
            zoneHeure.Visibility = modeHeure ? Visibility.Visible : Visibility.Collapsed;
            foreach (var puce in puces) Choisi(puce.Key, !modeHeure && Math.Abs(minutesChoisies - puce.Value) < 0.5);
            for (int i = 0; i < 3; i++) Choisi(boutonsAction[i], (int)action == i);
            texteHH.Text = heureCible.ToString("00");
            texteMM.Text = minuteCible.ToString("00");

            texteNote.Text = action == TypeAction.Veille
                ? L("Garde Crépuscule ouvert ou rangé près de l'horloge : c'est lui qui lancera la veille.",
                    "Keep Crépuscule open or hidden next to the clock: it is the one that triggers sleep.")
                : L("Windows s'en chargera même si tu fermes cette fenêtre. Les applis encore ouvertes seront fermées.",
                    "Windows will take care of it even if this window is closed. Apps still open will be closed.");

            langueFr.Background = anglais ? Brushes.Transparent : Verre(0.26);
            langueEn.Background = anglais ? Verre(0.26) : Brushes.Transparent;
        }

        void MettreAJourBarre()
        {
            string texte = enCours
                ? "Crépuscule · " + Nom() + L(" à ", " at ") + cibleUtc.ToLocalTime().ToString("HH:mm")
                : "Crépuscule · " + L("rien de programmé", "nothing scheduled");
            icone.Text = texte.Length > 63 ? texte.Substring(0, 63) : texte;
        }

        static void Fixer(TextBlock t, string texte)
        {
            if (t.Text != texte) t.Text = texte;
        }

        static double Borne(double v) { return Math.Max(0, Math.Min(1, v)); }

        static Color Melange(Color a, Color b, double t)
        {
            return Color.FromRgb((byte)(a.R + (b.R - a.R) * t), (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t));
        }

        static Color Palette(Color[] c, double t)
        {
            return t < 0.55 ? Melange(c[0], c[1], t / 0.55) : Melange(c[1], c[2], (t - 0.55) / 0.45);
        }

        // Appelé ~40 fois par seconde : compte à rebours + animation.
        void Tic()
        {
            double temps = chrono.Elapsed.TotalSeconds;
            double restant = enCours ? (cibleUtc - DateTime.UtcNow).TotalSeconds : 0;
            if (enCours && !averti && restant <= 300 && restant > 1) { averti = true; Avertir(); }
            if (enCours && restant <= 0) Terminer();

            if ((DateTime.UtcNow - dernierTexteBarre).TotalSeconds >= 1)
            {
                dernierTexteBarre = DateTime.UtcNow;
                MettreAJourBarre();
            }
            if (!IsVisible) return;

            double minutes = DureeActuelle();
            valeurAffichee += (minutes - valeurAffichee) * (glisse ? 0.45 : 0.18);
            if (Math.Abs(minutes - valeurAffichee) < 0.005) valeurAffichee = minutes;
            DessinerArc(Math.Min(valeurAffichee, MAX_CADRAN) / MAX_CADRAN);

            // Plus l'échéance approche, plus la nuit tombe.
            double nuitVisee = 1 - Math.Min(1, minutes / 120);
            nuit = nuit < 0 ? nuitVisee : nuit + (nuitVisee - nuit) * 0.04;
            stopHaut.Color = Palette(Haut, nuit);
            stopMilieu.Color = Palette(Milieu, nuit);
            stopBas.Color = Palette(Bas, nuit);
            double ciel = Borne((nuit - 0.55) / 0.45);
            for (int i = 0; i < etoiles.Count; i++)
                etoiles[i].Opacity = ciel * (0.55 + 0.45 * Math.Sin(temps * (0.8 + i % 5 * 0.3) + phases[i]));
            lune.Opacity = ciel;

            if (enCours && restant <= 300)
            {
                double pouls = 0.5 + 0.5 * Math.Sin(temps * 4);
                echelleSoleil.ScaleX = echelleSoleil.ScaleY = 1 + 0.14 * pouls;
                anneau.Opacity = 0.6 + 0.4 * pouls;
            }
            else
            {
                echelleSoleil.ScaleX = echelleSoleil.ScaleY = 1;
                anneau.Opacity = 1;
            }

            Fixer(texteHaut, Nom() + L(" dans", " in"));
            string duree = enCours && restant < 3600
                ? ((int)Math.Ceiling(Math.Max(0, restant)) / 60).ToString("00") + ":" + ((int)Math.Ceiling(Math.Max(0, restant)) % 60).ToString("00")
                : FormatDuree(enCours ? Math.Ceiling(restant / 60) : Math.Round(minutes));
            Fixer(texteDuree, duree);
            texteDuree.FontSize = duree.Length > 6 ? 36 : 44;
            var cible = CibleLocale();
            Fixer(texteBas, !enCours && minutes < 0.5
                ? L("fais glisser le soleil", "drag the sun")
                : L("à ", "at ") + cible.ToString("HH:mm") + (cible.Date > DateTime.Today ? L(" · demain", " · tomorrow") : ""));
            if (enCours) Fixer(textePoeme, Poeme(restant));
            boutonLancer.Opacity = minutes >= 1 ? 1 : 0.45;
            texteMessage.Opacity = 0.9 * Borne((messageJusqua - DateTime.UtcNow).TotalSeconds / 0.6);
        }

        void DessinerArc(double fraction)
        {
            double angle = Math.Min(fraction, 0.9995) * 2 * Math.PI;
            var fin = new Point(C + R * Math.Sin(angle), C - R * Math.Cos(angle));
            segmentArc.Point = fin;
            segmentArc.IsLargeArc = angle > Math.PI;
            arc.Visibility = fraction < 0.003 ? Visibility.Hidden : Visibility.Visible;
            Canvas.SetLeft(soleil, fin.X - 17);
            Canvas.SetTop(soleil, fin.Y - 17);
        }

        void ToucheClavier(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape) Reduire();
            else if (enCours) return;
            else if (e.Key == Key.Enter) Lancer();
            else if (e.Key == Key.Up || e.Key == Key.Right) Ajuster(5);
            else if (e.Key == Key.Down || e.Key == Key.Left) Ajuster(-5);
        }
    }
}
