﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using System.Linq;

namespace SuperImageEvolver {
    public class TaskState {
        public int Shapes, Vertices;
        public int ImageWidth, ImageHeight;

        public DNA BestMatch;

        public int ImprovementCounter, MutationCounter;

        public Bitmap Image;
        public BitmapData ImageData;
        public Bitmap BestMatchRender;
        public string ProjectFileName;

        public List<Mutation> MutationLog = new List<Mutation>();

        public IInitializer Initializer = new SegmentedInitializer(Color.Black);
        public IMutator Mutator = new HardMutator();
        public IEvaluator Evaluator = new RGBEvaluator( false );

        public DateTime TaskStart;
        public DateTime LastImprovementTime;
        public long LastImprovementMutationCount;
        public Point ClickLocation;

        public const int FormatVersion = 1;

        public object ImprovementLock = new object();

        public Dictionary<MutationType, int> MutationCounts = new Dictionary<MutationType, int>();
        public Dictionary<MutationType, double> MutationImprovements = new Dictionary<MutationType, double>();
        

        public void SetEvaluator( IEvaluator newEvaluator ) {
            lock( ImprovementLock ) {
                if( Image != null && BestMatch != null ) {
                    using( Bitmap testCanvas = new Bitmap( ImageWidth, ImageHeight ) ) {
                        newEvaluator.Initialize( this );
                        BestMatch.Divergence = newEvaluator.CalculateDivergence( testCanvas, BestMatch, this, 1 );
                    }
                }
                Evaluator = newEvaluator;
            }
        }

        public void Serialize( Stream stream ) {
            BinaryWriter writer = new BinaryWriter( stream );
            writer.Write( FormatVersion );
            writer.Write( Shapes );
            writer.Write( Vertices );
            BestMatch.Serialize( stream );
            writer.Write( ImprovementCounter );
            writer.Write( MutationCounter );
            writer.Write( DateTime.UtcNow.Subtract( TaskStart ).Ticks );

            //ModuleManager.WriteModule( Initializer, stream );
            //ModuleManager.WriteModule( Mutator, stream );
            //ModuleManager.WriteModule( Evaluator, stream );

            using( MemoryStream ms = new MemoryStream() ) {
                Image.Save( ms, ImageFormat.Png );
                ms.Flush();
                writer.Write( (int)ms.Length );
                writer.Write( ms.GetBuffer() );
            }

            writer.Write( MutationCounts.Count );
            foreach( MutationType mtype in Enum.GetValues(typeof(MutationType)) ) {
                writer.Write( mtype.ToString() );
                writer.Write( (int)MutationCounts[mtype] );
                writer.Write( (int)MutationImprovements[mtype] );
            }
        }

        public TaskState() {
            foreach( MutationType mutype in Enum.GetValues( typeof( MutationType ) ) ) {
                MutationCounts[mutype] = 0;
                MutationImprovements[mutype] = 0;
            }
        }

        public TaskState( Stream stream ) : this() {
            BinaryReader reader = new BinaryReader( stream );
            if( reader.ReadInt32() != FormatVersion ) throw new FormatException();
            Shapes = reader.ReadInt32();
            Vertices = reader.ReadInt32();
            BestMatch = new DNA( stream, Shapes, Vertices );
            ImprovementCounter = reader.ReadInt32();
            MutationCounter = reader.ReadInt32();
            TaskStart = DateTime.UtcNow.Subtract( TimeSpan.FromTicks( reader.ReadInt64() ) );

            Initializer = (IInitializer)ModuleManager.ReadModule( stream );
            Mutator = (IMutator)ModuleManager.ReadModule( stream );
            Evaluator = (IEvaluator)ModuleManager.ReadModule( stream );

            int imageLength = reader.ReadInt32();
            using( MemoryStream ms = new MemoryStream( reader.ReadBytes( imageLength ) ) ) {
                Image = new Bitmap( Bitmap.FromStream( ms ) );
            }

            int statCount = reader.ReadInt32();
            for( int i = 0; i < statCount; i++ ) {
                string statName = reader.ReadString();
                try {
                    MutationType mtype = (MutationType)Enum.Parse( typeof( MutationType ), statName );
                    MutationCounts[mtype] = reader.ReadInt32();
                    MutationImprovements[mtype] = reader.ReadDouble();
                } catch( ArgumentException ) { }
            }
        }

        public XDocument SerializeSVG() {
            XDocument doc = new XDocument();
            XNamespace svg = "http://www.w3.org/2000/svg";
            XElement root = new XElement( svg+"svg" );
            root.Add( new XAttribute( "xmlns", svg ) );
            root.Add( new XAttribute( XNamespace.Xmlns + "xlink", "http://www.w3.org/1999/xlink" ) );
            root.Add( new XAttribute( "width", ImageWidth ) );
            root.Add( new XAttribute( "height", ImageHeight ) );
            DNA currentBestMatch = BestMatch;
            foreach( Shape shape in currentBestMatch.Shapes ) {
                root.Add( shape.SerializeSVG( svg ) );
            }
            doc.Add( root );
            
            return doc;
        }
    }
}