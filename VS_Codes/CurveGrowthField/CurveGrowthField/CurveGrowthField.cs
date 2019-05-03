using System;
using System.Collections;
using System.Collections.Generic;

using Rhino;
using Rhino.Geometry;

using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
// <Custom using>
using Noises;
// </Custom using>

/// <summary>
/// This class will be instantiated on demand by the Script component.
/// </summary>
public class Script_Instance : GH_ScriptInstance
{
    #region Utility functions
    /// <summary>Print a String to the [Out] Parameter of the Script component.</summary>
    /// <param name="text">String to print.</param>
    private void Print(string text) { __out.Add(text); }
    /// <summary>Print a formatted String to the [Out] Parameter of the Script component.</summary>
    /// <param name="format">String format.</param>
    /// <param name="args">Formatting parameters.</param>
    private void Print(string format, params object[] args) { __out.Add(string.Format(format, args)); }
    /// <summary>Print useful information about an object instance to the [Out] Parameter of the Script component. </summary>
    /// <param name="obj">Object instance to parse.</param>
    private void Reflect(object obj) { __out.Add(GH_ScriptComponentUtilities.ReflectType_CS(obj)); }
    /// <summary>Print the signatures of all the overloads of a specific method to the [Out] Parameter of the Script component. </summary>
    /// <param name="obj">Object instance to parse.</param>
    private void Reflect(object obj, string method_name) { __out.Add(GH_ScriptComponentUtilities.ReflectType_CS(obj, method_name)); }
    #endregion

    #region Members
    /// <summary>Gets the current Rhino document.</summary>
    private RhinoDoc RhinoDocument;
    /// <summary>Gets the Grasshopper document that owns this script.</summary>
    private GH_Document GrasshopperDocument;
    /// <summary>Gets the Grasshopper script component that owns this script.</summary>
    private IGH_Component Component;
    /// <summary>
    /// Gets the current iteration count. The first call to RunScript() is associated with Iteration==0.
    /// Any subsequent call within the same solution will increment the Iteration count.
    /// </summary>
    private int Iteration;
    #endregion

    /// <summary>
    /// This procedure contains the user code. Input parameters are provided as regular arguments, 
    /// Output parameters as ref arguments. You don't have to assign output parameters, 
    /// they will have a default value.
    /// </summary>
    private void RunScript(bool reset, bool go, List<Point3d> StartingPositions, int MaxPtsCount, bool isClosed, int CollisionIterations, double CollisionDistance, double CollisionWeight, int LaplacianIterations, double LaplacianStrength, double FieldScale, double FieldOffset, double FieldWeight, ref object oCenters, ref object cDs)
    {
        // <Custom code> 

        if (reset || myCurveGrowth == null)
            myCurveGrowth = new CurveGrowth(StartingPositions, isClosed);

        if (go && myCurveGrowth.Points.Count < MaxPtsCount)
        {

            myCurveGrowth.MaxPtsCount = MaxPtsCount;
            myCurveGrowth.CollisionIterations = CollisionIterations;
            myCurveGrowth.CollisionDistance = CollisionDistance;
            myCurveGrowth.SamplingDistance = 0.7 * CollisionDistance;
            myCurveGrowth.CollisionWeight = CollisionWeight;
            myCurveGrowth.LaplacianIterations = LaplacianIterations;
            myCurveGrowth.LaplacianStrength = LaplacianStrength;
            myCurveGrowth.FieldScale = FieldScale;
            myCurveGrowth.FieldOffset = FieldOffset;
            myCurveGrowth.FieldWeight = FieldWeight;

            //myCurveGrowth.Update();
            myCurveGrowth.UpdateWithResampling();

            Component.ExpireSolution(true);
        }

        oCenters = myCurveGrowth.Points;
        cDs = myCurveGrowth.CollisionDistances;

        // </Custom code>
    }

    // <Custom additional code> 
    //List<Point3d> points;
    //RTree pointsRTree;
    CurveGrowth myCurveGrowth;

    public class CurveGrowth
    {
        private List<Point3d> points;
        public List<Point3d> Points
        { get { return points; } }
        private bool isClosed;

        private List<Vector3d> TotalMoves;
        private List<double> TotalWeights;
        public List<double> CollisionDistances;
        private RTree pointsRTree;

        public int MaxPtsCount;
        public int CollisionIterations;
        public double CollisionDistance;
        public double CollisionWeight;
        public int LaplacianIterations;
        public double LaplacianStrength;
        public double FieldScale;
        public double FieldOffset;
        public double FieldWeight;
        public double SamplingDistance;

        public CurveGrowth(List<Point3d> Points, bool isClosed)
        {
            points = Points;
            this.isClosed = isClosed;
        }

        public void Update()
        {
            InitializeUpdateFields();
            PopulateFixedCollisionDistances();

            MoveField();
            LaplacianSmooth();
            ProcessCollisionsStatic();
            Grow();
        }

        public void UpdateWithResampling()
        {
            Resample();
            LaplacianSmooth();
            InitializeUpdateFields();

            //MoveField();
            PopulateDynamicCollisionDistances();
            for (int i = 0; i < CollisionIterations; i++)
                ProcessCollisions();
            //ProcessCollisionsStatic();
        }

        private void InitializeUpdateFields()
        {
            TotalMoves = new List<Vector3d>();
            TotalWeights = new List<double>();

            //prepare RTree
            pointsRTree = RTree.CreateFromPointArray(points);

            // prepare move vectors and collision count lists
            for (int i = 0; i < points.Count; i++)
            {
                TotalMoves.Add(new Vector3d(0.0, 0.0, 0.0));
                TotalWeights.Add(0.0);
            }
        }

        private void PopulateFixedCollisionDistances()
        {
            CollisionDistances = new List<double>();
            foreach (Point3d p in points)
                CollisionDistances.Add(CollisionDistance);
        }

        private void PopulateDynamicCollisionDistances()
        {
            CollisionDistances = new List<double>();
            double noiseP;
            foreach (Point3d p in points)
            {
                noiseP = (double)Noise.Generate((float)(p.X * FieldScale + FieldOffset), (float)(p.Y * FieldScale + FieldOffset));
                noiseP = Remap(noiseP, -1.0, 1.0, 0.3 * CollisionDistance, CollisionDistance);
                CollisionDistances.Add(noiseP);
                //CollisionDistances.Add(CollisionDistance);
            }
        }

        private void ProcessCollisionsStatic()
        {
            // find collisions counts and sums moving vectors
            for (int i = 0; i < points.Count; i++)
            {
                List<int> neighbours = new List<int>();
                Sphere searchSphere = new Sphere(points[i], CollisionDistance);

                // Eventhandler callback function for Planes RTree search
                EventHandler<RTreeEventArgs> pointsRTreeCallback = (sender, args) =>
                {
                    if (i < args.Id) neighbours.Add(args.Id);
                };
                //                      Sphere(  center  ,      radius      ), callback function
                pointsRTree.Search(searchSphere, pointsRTreeCallback);

                if (neighbours.Count == 0) continue;
                foreach (int j in neighbours)
                {
                    Vector3d move = points[i] - points[j];
                    double currentDistance = move.Length;

                    move *= 0.5 * (CollisionDistance - currentDistance) / currentDistance;
                    TotalMoves[i] += CollisionWeight * move;
                    TotalMoves[j] -= CollisionWeight * move;
                    TotalWeights[i] += CollisionWeight;
                    TotalWeights[j] += CollisionWeight;
                }
            }

            for (int i = 0; i < points.Count; i++)
                if (TotalWeights[i] != 0.0)
                    points[i] += TotalMoves[i] / TotalWeights[i];
        }

        private void ProcessCollisions()
        {
            // find collisions counts and sums moving vectors
            for (int i = 0; i < points.Count; i++)
            {
                List<int> neighbours = new List<int>();
                Sphere searchSphere = new Sphere(points[i], CollisionDistances[i]);

                // Eventhandler callback function for Planes RTree search
                EventHandler<RTreeEventArgs> pointsRTreeCallback = (sender, args) =>
                {
                    if (i < args.Id) neighbours.Add(args.Id);
                };
                //                      Sphere(  center  ,      radius      ), callback function
                pointsRTree.Search(searchSphere, pointsRTreeCallback);

                if (neighbours.Count == 0) continue;
                foreach (int j in neighbours)
                {
                    Vector3d move = points[i] - points[j];

                    double currentDistance = move.Length;
                    double invertedSum = 1.0 / (CollisionDistances[i] + CollisionDistances[j]);

                    Vector3d movei = move * CollisionDistances[i] * invertedSum * (CollisionDistances[i] - currentDistance) / currentDistance;
                    TotalMoves[i] += CollisionWeight * movei;
                    TotalWeights[i] += CollisionWeight;
                    if (currentDistance < CollisionDistances[j])
                    {
                        Vector3d movej = move * CollisionDistances[j] * invertedSum * (CollisionDistances[j] - currentDistance) / currentDistance;
                        TotalMoves[j] -= CollisionWeight * movej;
                        TotalWeights[j] += CollisionWeight;
                    }
                }
            }

            for (int i = 0; i < points.Count; i++)
                if (TotalWeights[i] != 0.0)
                    points[i] += TotalMoves[i] / TotalWeights[i];
        }

        private void MoveField()
        {
            for (int i = 0; i < points.Count; i++)
            {
                Vector3d move = CurlNoise.CurlNoiseVector(points[i], FieldScale, FieldOffset, false);
                TotalMoves[i] += move * FieldWeight;
                TotalWeights[i] += FieldWeight;
            }
        }

        private void Grow()
        {
            if (points.Count < MaxPtsCount)
            {
                /*
                 * why this works:
                 * when you insert an element in a list, the ones after that index are
                 * pushed one slot forward. It is crucial to have a condition that does
                 * not generate an infinite amount of elements during the same loop
                 */
                int iPlusOne;
                for (int i = 0; i < (isClosed ? points.Count : points.Count - 1); i++)
                {
                    iPlusOne = (i + 1) % points.Count;
                    if (points[i].DistanceTo(points[iPlusOne]) > CollisionDistance - 0.1)
                    {
                        Point3d newCenter = 0.5 * (points[iPlusOne] + points[i]);
                        points.Insert(iPlusOne, newCenter); // see slide 89
                    }
                }

            }
        }

        private void LaplacianSmooth()
        {
            List<Point3d> pts = points;
            int lastPtIndex = pts.Count - 1;
            double oneLessFactor = 1 - LaplacianStrength;

            List<Point3d> newPts = pts;

            if (isClosed)
            {

                //pts.RemoveAt(pts.Count - 1);
                while (LaplacianIterations-- > 0)            // decreases at each cycle
                {
                    newPts = new List<Point3d>(); // newPts becomes a new list
                    for (int i = 0; i < pts.Count; i++)
                    {
                        Point3d pt = pts[i % pts.Count];

                        Point3d newPt = (pts[(i - 1 + pts.Count) % pts.Count] + pts[(i + 1) % pts.Count]) * 0.5;
                        newPt = newPt * LaplacianStrength + pt * oneLessFactor;
                        newPts.Add(newPt);
                    }

                    pts = newPts;                 // exchange pts with newPts
                }
            }
            else
            {
                while (LaplacianIterations-- > 0)            // decreases at each cycle
                {
                    newPts = new List<Point3d>(); // newPts becomes a new list

                    for (int i = 0; i < pts.Count; i++)
                    {
                        Point3d pt = pts[i];
                        if (i == 0 || i == lastPtIndex)
                        {
                            newPts.Add(pt);
                            continue;
                        }

                        Point3d newPt = (pts[i - 1] + pts[i + 1]) * 0.5;
                        newPt = newPt * LaplacianStrength + pt * oneLessFactor;
                        newPts.Add(newPt);
                    }

                    pts = newPts;                 // exchange pts with newPts
                }
            }

            points = pts;
        }

        private void Resample()
        {
            Curve interpolate;
            List<Point3d> interp = new List<Point3d>(points);
            if (isClosed)
            {
                if (interp[interp.Count - 1] != interp[0]) interp.Add(points[0]);
                interpolate = Curve.CreateInterpolatedCurve(interp, 3, CurveKnotStyle.UniformPeriodic);
            }
            else
                interpolate = Curve.CreateInterpolatedCurve(interp, 3, CurveKnotStyle.Uniform);

            double[] tPars = interpolate.DivideByLength(SamplingDistance, true);

            points.Clear();

            // rebuild point list
            for (int i = 0; i < tPars.Length; i++)
                points.Add(interpolate.PointAt(tPars[i]));
        }

        private double Remap(double val, double from1, double to1, double from2, double to2)
        {
            return (val - from1) / (to1 - from1) * (to2 - from2) + from2;
        }
    }




    #region Old

    //public void CurlNoiseVector2(Point3d P, double S, double t, bool is3D, out Vector3d V)
    //{

    //    float fS = (float)S;
    //    float fT = (float)t;
    //    float nX = (float)P.X * fS + fT; // num5
    //    float nY = (float)P.Y * fS + fT; // num6
    //    float nZ = (float)P.Z * fS + fT; // num7

    //    double num8 = Noise.Generate(nX, nY, nZ);
    //    float dT = 1.0f;
    //    float nPlus = Noise.Generate(nX, nY + dT, nZ);
    //    float nMinus = Noise.Generate(nX, nY - dT, nZ);
    //    float nDiff = (nPlus - nMinus) / (2f * dT);
    //    nPlus = Noise.Generate(nX, nY, nZ + dT);
    //    nMinus = Noise.Generate(nX, nY, nZ - dT);
    //    float num13 = (nPlus - nMinus) / (2f * dT);
    //    float num14 = nDiff - num13;
    //    nX -= fT;
    //    nY -= fT;
    //    nPlus = Noise.Generate(nX, nY, nZ + dT);
    //    nMinus = Noise.Generate(nX, nY, nZ - dT);
    //    nDiff = (nPlus - nMinus) / (2f * dT);
    //    nPlus = Noise.Generate(nX + dT, nY, nZ);
    //    nMinus = Noise.Generate(nX - dT, nY, nZ);
    //    num13 = (nPlus - nMinus) / (2f * dT);
    //    float num15 = nDiff - num13;
    //    nPlus = Noise.Generate(nX + dT, nY, nZ);
    //    nMinus = Noise.Generate(nX - dT, nY, nZ);
    //    nDiff = (nPlus - nMinus) / (2f * dT);
    //    nPlus = Noise.Generate(nX, nY + dT, nZ);
    //    nMinus = Noise.Generate(nX, nY - dT, nZ);
    //    num13 = (nPlus - nMinus) / (2f * dT);
    //    float num16 = nDiff - num13;
    //    Vector3d val2 = new Vector3d((double)num14, (double)num15, (double)num16);
    //    if (!is3D)
    //    {
    //        //debug += "nX2d: " + nX.ToString() + "\n";
    //        //debug += "nY2d: " + nY.ToString() + "\n";
    //        //debug += "dT2d: " + dT.ToString() + "\n";
    //        //nPlus = Noise.Generate(nX, nY + dT);
    //        //debug += "nPlus: " + nPlus.ToString() + "\n";
    //        //nMinus = 0;// Noise.Generate(nX, nY - dT);
    //        //debug += "nMinus: " + nMinus.ToString() + "\n";
    //        //nDiff = (nPlus - nMinus) / (2f * dT);
    //        nPlus = Noise.Generate(nX + dT, nY);
    //        nMinus = Noise.Generate(nX - dT, nY);
    //        nDiff = (nPlus - nMinus) / (2f * dT);
    //        val2 = new Vector3d((double)nDiff, (double)(-num13), 0.0);
    //    }
    //    V = new Vector3d(val2);
    //    //val2.Unitize();
    //    //int red = (int)Math.Floor(Remap((float)val2.X, -1f, 1f, 0f, 255f));
    //    //int green = (int)Math.Floor(Remap((float)val2.Y, -1f, 1f, 0f, 255f));
    //    //int blue = (int)Math.Floor(Remap((float)val2.Z, -1f, 1f, 0f, 255f));
    //    //C = new GH_Colour(Color.FromArgb(red, green, blue));

    //}

    #endregion

    // </Custom additional code> 

    private List<string> __err = new List<string>(); //Do not modify this list directly.
    private List<string> __out = new List<string>(); //Do not modify this list directly.
    private RhinoDoc doc = RhinoDoc.ActiveDoc;       //Legacy field.
    private IGH_ActiveObject owner;                  //Legacy field.
    private int runCount;                            //Legacy field.

    public override void InvokeRunScript(IGH_Component owner, object rhinoDocument, int iteration, List<object> inputs, IGH_DataAccess DA)
    {
        //Prepare for a new run...
        //1. Reset lists
        this.__out.Clear();
        this.__err.Clear();

        this.Component = owner;
        this.Iteration = iteration;
        this.GrasshopperDocument = owner.OnPingDocument();
        this.RhinoDocument = rhinoDocument as Rhino.RhinoDoc;

        this.owner = this.Component;
        this.runCount = this.Iteration;
        this.doc = this.RhinoDocument;

        //2. Assign input parameters
        bool iReset = default(bool);
        if (inputs[0] != null)
        {
            iReset = (bool)(inputs[0]);
        }

        List<Point3d> iStartingPositions = null;
        if (inputs[1] != null)
        {
            iStartingPositions = GH_DirtyCaster.CastToList<Point3d>(inputs[1]);
        }
        int iMaxCenterCount = default(int);
        if (inputs[2] != null)
        {
            iMaxCenterCount = (int)(inputs[2]);
        }



        //3. Declare output parameters
        object oCenters = null;


        //4. Invoke RunScript
        RunScript(iReset, iStartingPositions, iMaxCenterCount, ref oCenters);

        try
        {
            //5. Assign output parameters to component...
            if (oCenters != null)
            {
                if (GH_Format.TreatAsCollection(oCenters))
                {
                    IEnumerable __enum_oCenters = (IEnumerable)(oCenters);
                    DA.SetDataList(1, __enum_oCenters);
                }
                else
                {
                    if (oCenters is Grasshopper.Kernel.Data.IGH_DataTree)
                    {
                        //merge tree
                        DA.SetDataTree(1, (Grasshopper.Kernel.Data.IGH_DataTree)(oCenters));
                    }
                    else
                    {
                        //assign direct
                        DA.SetData(1, oCenters);
                    }
                }
            }
            else
            {
                DA.SetData(1, null);
            }

        }
        catch (Exception ex)
        {
            this.__err.Add(string.Format("Script exception: {0}", ex.Message));
        }
        finally
        {
            //Add errors and messages... 
            if (owner.Params.Output.Count > 0)
            {
                if (owner.Params.Output[0] is Grasshopper.Kernel.Parameters.Param_String)
                {
                    List<string> __errors_plus_messages = new List<string>();
                    if (this.__err != null) { __errors_plus_messages.AddRange(this.__err); }
                    if (this.__out != null) { __errors_plus_messages.AddRange(this.__out); }
                    if (__errors_plus_messages.Count > 0)
                        DA.SetDataList(0, __errors_plus_messages);
                }
            }
        }
    }
}