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
using Plankton;
using PlanktonGh;
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
    private void RunScript(bool reset, bool go, bool grow, bool useRTree, Mesh M, int SubIterationCount, int MaxVertexCount, Line MouseLine, Point3d Attractor, Line Obstacles, double EdgeLengthWeight, double CollisionDistance, double CollisionWeight, double BendingReistanceWeight, ref object oMesh)
    {
        // <Custom code>

        // return if no mesh is provided
        if (M == null) return;

        // initialize system on reset button or if the system has never been defined (first run)
        if (reset || myMeshGrowthSystem == null)
            myMeshGrowthSystem = new MeshGrowthSystem(M);

        // pass live parameters to the simulation system
        myMeshGrowthSystem.Grow = grow;
        myMeshGrowthSystem.MaxVertexCount = MaxVertexCount;
        myMeshGrowthSystem.EdgeLenghtConstrainWeight = EdgeLengthWeight;
        myMeshGrowthSystem.CollisionWeight = CollisionWeight;
        myMeshGrowthSystem.BendingResistanceWeight = BendingReistanceWeight;
        myMeshGrowthSystem.UseRTree = useRTree;
        myMeshGrowthSystem.CollisionDistance = CollisionDistance;

        // update simulation
        if (go)
        {
            for (int i = 0; i < SubIterationCount; i++)
                myMeshGrowthSystem.Update();
            Component.ExpireSolution(true);
        }

        // output data
        oMesh = myMeshGrowthSystem.GetRhinoMesh();

        // </Custom code>
    }

    // <Custom additional code> 

    // global variables
    public MeshGrowthSystem myMeshGrowthSystem;

    /// <summary>
    /// This is the simulation system class
    /// </summary>
    public class MeshGrowthSystem
    {
        private PlanktonMesh ptMesh;

        public bool Grow = false;
        public int MaxVertexCount;

        public bool UseRTree;

        public double EdgeLenghtConstrainWeight;
        public double CollisionDistance;
        public double CollisionWeight;
        public double BendingResistanceWeight;


        private List<Vector3d> totalWeightedMoves;
        private List<double> totalWeights;


        public MeshGrowthSystem(Mesh startingMesh)
        {
            ptMesh = startingMesh.ToPlanktonMesh(); // converts a Meesh into a Plankton Mesh
        }

        public Mesh GetRhinoMesh()
        {
            return ptMesh.ToRhinoMesh(); // converts a Plankton Mesh into a Mesh
        }

        public void Update()
        {
            // splitting logic
            if (Grow) SplitAllLongEdges();

            // moving vertex away
            // initialize Weight vectors and numbers
            totalWeightedMoves = new List<Vector3d>();
            totalWeights = new List<double>();

            for (int i = 0; i < ptMesh.Vertices.Count; i++)
            {
                totalWeightedMoves.Add(new Vector3d(0, 0, 0));
                totalWeights.Add(0.0);
            }

            if (UseRTree) ProcessCollisionsUsingRTree();
            else ProcessCollisions();
            ProcessBendingReistance();
            ProcessEdgeLengthConstraint();

            UpdateVertexPositions();

        }

        private void ProcessEdgeLengthConstraint()
        {
            for (int k = 0; k < ptMesh.Halfedges.Count; k += 2)
            {
                int i = ptMesh.Halfedges[k].StartVertex;
                int j = ptMesh.Halfedges[k + 1].StartVertex;

                Point3d vI = ptMesh.Vertices[i].ToPoint3d();
                Point3d vJ = ptMesh.Vertices[j].ToPoint3d();

                if (vI.DistanceTo(vJ) < CollisionDistance) continue;

                Vector3d move = vJ - vI;
                move *= (move.Length - CollisionDistance) * 0.5 / move.Length;

                totalWeightedMoves[i] += move * EdgeLenghtConstrainWeight;
                totalWeightedMoves[j] -= move * EdgeLenghtConstrainWeight;
                totalWeights[i] += EdgeLenghtConstrainWeight;
                totalWeights[j] += EdgeLenghtConstrainWeight;
            }
        }

        private void ProcessBendingReistance()
        {
            int halfEdgeCount = ptMesh.Halfedges.Count;

            for (int k = 0; k < halfEdgeCount; k += 2)
            {
                // see day03 slides, pag. 25 for a graphic explanation

                // finding adjacent faces vertices
                int i = ptMesh.Halfedges[k].StartVertex;
                int j = ptMesh.Halfedges[k + 1].StartVertex;
                int p = ptMesh.Halfedges[ptMesh.Halfedges[k].PrevHalfedge].StartVertex;
                int q = ptMesh.Halfedges[ptMesh.Halfedges[k + 1].PrevHalfedge].StartVertex;

                Point3d vI = ptMesh.Vertices[i].ToPoint3d();
                Point3d vJ = ptMesh.Vertices[j].ToPoint3d();
                Point3d vP = ptMesh.Vertices[p].ToPoint3d();
                Point3d vQ = ptMesh.Vertices[q].ToPoint3d();

                // normals of adjacent faces
                Vector3d nP = Vector3d.CrossProduct(vJ - vI, vP - vI);
                // Vector3d nQ = Vector3d.CrossProduct(vJ - vI, vQ - vI); // this was wrong, as the normal was flipped
                Vector3d nQ = Vector3d.CrossProduct(vQ - vI, vJ - vI);

                // building target plane
                Vector3d planeNormal = nP + nQ;
                Point3d planeOrigin = 0.25 * (vI + vJ + vP + vQ);
                Plane plane = new Plane(planeOrigin, planeNormal);

                // compute move vectors
                totalWeightedMoves[i] += BendingResistanceWeight * (plane.ClosestPoint(vI) - vI);
                totalWeightedMoves[j] += BendingResistanceWeight * (plane.ClosestPoint(vJ) - vJ);
                totalWeightedMoves[p] += BendingResistanceWeight * (plane.ClosestPoint(vP) - vP);
                totalWeightedMoves[q] += BendingResistanceWeight * (plane.ClosestPoint(vQ) - vQ);
                totalWeights[i] += BendingResistanceWeight;
                totalWeights[j] += BendingResistanceWeight;
                totalWeights[p] += BendingResistanceWeight;
                totalWeights[q] += BendingResistanceWeight;


            }
        }

        private void SplitAllLongEdges()
        {
            int halfEdgeCount = ptMesh.Halfedges.Count;

            for (int k = 0; k < halfEdgeCount; k += 2)
            {
                if (ptMesh.Vertices.Count < MaxVertexCount &&
                    ptMesh.Halfedges.GetLength(k) > 0.99 * CollisionDistance)
                {
                    SplitEdge(k);
                }

            }
        }

        private void ProcessCollisions()
        {
            int VertexCount = ptMesh.Vertices.Count;

            for (int i = 0; i < VertexCount - 1; i++)
                for (int j = i + 1; j < VertexCount; j++)
                {
                    // move is [j] - [i] cause the vector tip is always in the first term of subtraction
                    Vector3d move = ptMesh.Vertices[j].ToPoint3d() - ptMesh.Vertices[i].ToPoint3d();
                    double currentDistance = move.Length;
                    // if farther than collision distance, do nothing and go to the next loop iteration
                    if (currentDistance > CollisionDistance) continue;

                    // otherwise, repulsion logic applies as follows
                    move *= 0.5 * (currentDistance - CollisionDistance) / currentDistance;
                    totalWeightedMoves[i] += CollisionWeight * move;
                    totalWeightedMoves[j] -= CollisionWeight * move;
                    totalWeights[i] += CollisionWeight;
                    totalWeights[j] += CollisionWeight;
                }
        }

        private void ProcessCollisionsUsingRTree()
        {
            RTree rTree = new RTree();

            for (int i = 0; i < ptMesh.Vertices.Count; i++)
            {
                rTree.Insert(ptMesh.Vertices[i].ToPoint3d(), i);
            }

            for (int i = 0; i < ptMesh.Vertices.Count; i++)
            {
                Point3d vI = ptMesh.Vertices[i].ToPoint3d();
                Sphere searchSphere = new Sphere(vI, CollisionDistance);

                List<int> collisionIndices = new List<int>();

                // EventHandler<RTreeEventArgs> rTreeCallback (object sender, EventArgs args) => { };

                rTree.Search(searchSphere,
                   (sender, args) => { if (i < args.Id) collisionIndices.Add(args.Id); }); // rTreeCallback integrated in search statement
                                                                                           //                           | since in collisions we process both vertices involved I need to consider
                                                                                           //                             only a half of the corresponding matrix (otherwise I process each vertex twice)
                                                                                           //                             I consider only those vertices that haven't been processed yet (index > i)
                                                                                           //                             (this is also called "football team matching" logic)

                foreach (int j in collisionIndices)
                {
                    // move is [j] - [i] cause the vector tip is always in the first term of subtraction
                    Vector3d move = ptMesh.Vertices[i].ToPoint3d() - ptMesh.Vertices[j].ToPoint3d();
                    double currentDistance = move.Length;
                    // no need to check CollisionDistance here (it's taken care of by RTree)
                    // otherwise, repulsion logic applies as follows
                    move *= 0.5 * (CollisionDistance - currentDistance) / currentDistance;
                    totalWeightedMoves[i] += CollisionWeight * move;
                    totalWeightedMoves[j] -= CollisionWeight * move;
                    totalWeights[i] += CollisionWeight;
                    totalWeights[j] += CollisionWeight;
                }
            }
        }

        private void UpdateVertexPositions()
        {
            for (int i = 0; i < ptMesh.Vertices.Count; i++)
            {
                if (totalWeights[i] == 0.0) continue; // avoid division by 0

                Vector3d move = totalWeightedMoves[i] / totalWeights[i];
                Point3d newPosition = ptMesh.Vertices[i].ToPoint3d() + move;
                ptMesh.Vertices.SetVertex(i, newPosition.X, newPosition.Y, newPosition.Z);
            }
        }


        // method pasted from Long's codestarter
        private void SplitEdge(int edgeIndex)
        {
            int newHalfEdgeIndex = ptMesh.Halfedges.SplitEdge(edgeIndex);

            ptMesh.Vertices.SetVertex(
                ptMesh.Vertices.Count - 1,
                0.5 * (ptMesh.Vertices[ptMesh.Halfedges[edgeIndex].StartVertex].ToPoint3d() + ptMesh.Vertices[ptMesh.Halfedges[edgeIndex + 1].StartVertex].ToPoint3d()));

            if (ptMesh.Halfedges[edgeIndex].AdjacentFace >= 0)
                ptMesh.Faces.SplitFace(newHalfEdgeIndex, ptMesh.Halfedges[edgeIndex].PrevHalfedge);

            if (ptMesh.Halfedges[edgeIndex + 1].AdjacentFace >= 0)
                ptMesh.Faces.SplitFace(edgeIndex + 1, ptMesh.Halfedges[ptMesh.Halfedges[edgeIndex + 1].NextHalfedge].NextHalfedge);
        }


    }

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
        bool reset = default(bool);
        if (inputs[0] != null)
        {
            reset = (bool)(inputs[0]);
        }

        bool go = default(bool);
        if (inputs[1] != null)
        {
            go = (bool)(inputs[1]);
        }

        bool grow = default(bool);
        if (inputs[2] != null)
        {
            grow = (bool)(inputs[2]);
        }

        bool useRTree = default(bool);
        if (inputs[3] != null)
        {
            useRTree = (bool)(inputs[3]);
        }

        Mesh M = default(Mesh);
        if (inputs[4] != null)
        {
            M = (Mesh)(inputs[4]);
        }

        int SubIterationCount = default(int);
        if (inputs[5] != null)
        {
            SubIterationCount = (int)(inputs[5]);
        }

        int MaxVertexCount = default(int);
        if (inputs[6] != null)
        {
            MaxVertexCount = (int)(inputs[6]);
        }

        Line MouseLine = default(Line);
        if (inputs[7] != null)
        {
            MouseLine = (Line)(inputs[7]);
        }

        Point3d Attractor = default(Point3d);
        if (inputs[8] != null)
        {
            Attractor = (Point3d)(inputs[8]);
        }

        Line Obstacles = default(Line);
        if (inputs[9] != null)
        {
            Obstacles = (Line)(inputs[9]);
        }

        double EdgeLengthWeight = default(double);
        if (inputs[10] != null)
        {
            EdgeLengthWeight = (double)(inputs[10]);
        }

        double CollisionDistance = default(double);
        if (inputs[11] != null)
        {
            CollisionDistance = (double)(inputs[11]);
        }

        double CollisionWeight = default(double);
        if (inputs[12] != null)
        {
            CollisionWeight = (double)(inputs[12]);
        }

        double BendingReistanceWeight = default(double);
        if (inputs[13] != null)
        {
            BendingReistanceWeight = (double)(inputs[13]);
        }



        //3. Declare output parameters
        object oMesh = null;


        //4. Invoke RunScript
        RunScript(reset, go, grow, useRTree, M, SubIterationCount, MaxVertexCount, MouseLine, Attractor, Obstacles, EdgeLengthWeight, CollisionDistance, CollisionWeight, BendingReistanceWeight, ref oMesh);

        try
        {
            //5. Assign output parameters to component...
            if (oMesh != null)
            {
                if (GH_Format.TreatAsCollection(oMesh))
                {
                    IEnumerable __enum_oMesh = (IEnumerable)(oMesh);
                    DA.SetDataList(1, __enum_oMesh);
                }
                else
                {
                    if (oMesh is Grasshopper.Kernel.Data.IGH_DataTree)
                    {
                        //merge tree
                        DA.SetDataTree(1, (Grasshopper.Kernel.Data.IGH_DataTree)(oMesh));
                    }
                    else
                    {
                        //assign direct
                        DA.SetData(1, oMesh);
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