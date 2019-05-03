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
using System.Linq;
using SpatialSlur.SlurCore;
using SpatialSlur.SlurData;
using SpatialSlur.SlurMesh;
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
    private void RunScript(System.Object heMesh, double edgeLength, List<double> weights, double growthRate, int subSteps, bool reset, bool go, ref object result)
    {
        // <Custom code>

        // reset
        if (reset)
        {
            _mesh = null;
            return;
        }

        //
        if (_mesh == null)
        {
            InitDynamics((HeMesh)heMesh, edgeLength);
        }

        // update live fields
        _lengthWeight = weights[0];
        _collideWeight = weights[1];
        _smoothWeight = weights[2];
        _boundaryWeight = weights[3];

        _growthRate = growthRate;
        _subSteps = subSteps;

        // physics step
        Step();

        // output
        result = _mesh;

        // debug
        Print(String.Format("{0} steps", _stepCount));
        // Print(String.Format("{0} grid rebuilds", _gridRebuilds));

        // expire if playing
        if (go)
            Component.ExpireSolution(true);

        // </Custom code>

    }

    // <Custom additional code> 

    /*
    Differential surface growth simulation

    David Reeves
    www.spatialslur.com
    */

    HeMesh _mesh;
    HeVertexList _verts;
    HalfEdgeList _edges;
    IList<HalfEdge> _holes; // first edge of each hole in the mesh

    // vertex attributes
    List<Vec3d> _velocities;
    List<Vec3d> _moveSums;
    List<double> _weightSums;
    List<int> _degrees;

    // edge attributes
    List<double> _restLengths;
    List<int> _spinOrder;

    SpatialGrid3d<int> _grid;
    double _radius = 1.0;
    double _splitLength;
    double _targetScale;

    double _growthRate = 0.01;
    double _lengthWeight = 1.0;
    double _collideWeight = 0.1;
    double _smoothWeight = 10.0;
    double _boundaryWeight = 0.01;
    double _decay = 0.2;
    int _refineFreq = 3;

    Random _random; // used to shuffle spin order
    double _timeStep = 1.0;
    int _subSteps = 10;
    int _stepCount = 0;
    int _gridRebuilds = 0;


    //
    void InitDynamics(HeMesh mesh, double splitLength)
    {
        _mesh = new HeMesh(mesh);

        InitMeshAttributes();
        InitGrid(splitLength);

        _holes = GetHoles();
        _random = new Random(1);
        _stepCount = 0;
        _gridRebuilds = 0;
    }


    //
    void InitMeshAttributes()
    {
        _verts = _mesh.Vertices;
        _edges = _mesh.HalfEdges;

        // vertex attributes
        int nv = _verts.Count;
        _velocities = new List<Vec3d>(new Vec3d[nv]);
        _moveSums = new List<Vec3d>(new Vec3d[nv]);
        _weightSums = new List<double>(new double[nv]);
        _degrees = new List<int>(new int[nv]);

        // edge attributes
        _restLengths = new List<double>(_edges.GetEdgeLengths());
        _spinOrder = new List<int>(Enumerable.Range(0, _edges.Count >> 1));
    }


    /// <summary>
    ///
    /// </summary>
    /// <returns></returns>
    void InitGrid(double splitLength)
    {
        _splitLength = splitLength;
        _radius = _splitLength * 2.0 / 3.0; // * 0.5 - 1.0
        _targetScale = _radius;

        Domain3d d = new Domain3d();
        for (int i = 0; i < _verts.Count; i++)
            d.Include(_verts[i].Position);

        int nx = (int)Math.Ceiling(d.x.Span / _targetScale);
        int ny = (int)Math.Ceiling(d.y.Span / _targetScale);
        int nz = (int)Math.Ceiling(d.z.Span / _targetScale);

        _grid = new SpatialGrid3d<int>(d, nx, ny, nz);
    }


    /// <summary>
    /// Collects the first edge of each hole in the mesh.
    /// </summary>
    /// <returns></returns>
    List<HalfEdge> GetHoles()
    {
        List<HalfEdge> result = new List<HalfEdge>();
        bool[] visited = new bool[_edges.Count >> 1];

        for (int i = 0; i < _edges.Count; i++)
        {
            HalfEdge e = _edges[i];
            if (e.IsUnused || e.Face != null || visited[e.Index >> 1]) continue;

            // add to result
            result.Add(e);

            // flag edges in loop as visited
            foreach (HalfEdge ef in e.CirculateFace)
                visited[ef.Index >> 1] = true;
        }

        return result;
    }


    /// <summary>
    ///
    /// </summary>
    /// <returns></returns>
    void Step()
    {
        for (int i = 0; i < _subSteps; i++)
        {
            CalculateForces();
            UpdateVertices();
            _stepCount++;

            UpdateRestLengths();
            UpdateGrid();

            if (_refineFreq > 0 && _stepCount % _refineFreq == 0)
                RefineMesh();
        }
    }


    /// <summary>
    ///
    /// </summary>
    /// <returns></returns>
    void UpdateRestLengths()
    {
        var chunks = System.Collections.Concurrent.Partitioner.Create(0, _restLengths.Count);
        System.Threading.Tasks.Parallel.ForEach(chunks, range =>
          {
              for (int i = range.Item1; i < range.Item2; i++)
                  _restLengths[i] += _growthRate;
          });
    }


    /// <summary>
    /// Upddates the bounds of the grid.
    /// Also updates the resolution if the bin scale has become too large.
    /// </summary>
    /// <returns></returns>
    void UpdateGrid()
    {
        // get new bounds
        Domain3d d = new Domain3d();
        for (int i = 0; i < _verts.Count; i++)
            d.Include(_verts[i].Position);

        // set bounds of grid
        _grid.Domain = d;
        double dx = _grid.BinScaleX;
        double dy = _grid.BinScaleY;
        double dz = _grid.BinScaleZ;

        // create new grid if bin scale is too large in any dimension
        double t = _targetScale * 2.0;
        if (dx > t || dy > t || dz > t)
        {
            int nx = (int)Math.Ceiling(d.x.Span / _targetScale);
            int ny = (int)Math.Ceiling(d.y.Span / _targetScale);
            int nz = (int)Math.Ceiling(d.z.Span / _targetScale);
            _grid = new SpatialGrid3d<int>(d, nx, ny, nz);
            _gridRebuilds++;
        }
    }


    /// <summary>
    /// Calculates all forces in the system
    /// </summary>
    /// <returns></returns>
    void CalculateForces()
    {
        ConstrainEdgeLengths(_lengthWeight);

        LaplacianSmooth(_smoothWeight);
        BoundarySmooth(_boundaryWeight);

        Collide(_collideWeight);
        CollideCancel(_collideWeight);
        //CollideBrute(_collideWeight);
    }


    /// <summary>
    ///
    /// </summary>
    /// <param name="weight"></param>
    /// <returns></returns>
    void ConstrainEdgeLengths(double weight)
    {
        for (int i = 0; i < _edges.Count; i += 2)
        {
            HalfEdge e = _edges[i];
            if (e.IsUnused) continue;

            Vec3d move = e.Span;
            double d = move.Length;

            if (d > 0.0)
            {
                move *= (d - _restLengths[i >> 1]) / d * weight;

                int vi = e.Start.Index;
                _moveSums[vi] += move;
                _weightSums[vi] += weight;

                vi = e.End.Index;
                _moveSums[vi] -= move;
                _weightSums[vi] += weight;
            }
        }
    }


    /// <summary>
    ///
    /// </summary>
    /// <param name="weight"></param>
    /// <returns></returns>
    void LaplacianSmooth(double weight)
    {
        var chunks = System.Collections.Concurrent.Partitioner.Create(0, _verts.Count);
        System.Threading.Tasks.Parallel.ForEach(chunks, range =>
          {
              for (int i = range.Item1; i < range.Item2; i++)
              {
                  HeVertex v = _verts[i];
                  if (v.IsBoundary) continue; // skip boundary vertices

              // get average position of 1 ring neighbours
              Vec3d sum = new Vec3d();
                  int n = 0;
                  foreach (HeVertex cv in v.ConnectedVertices)
                  {
                      sum += cv.Position;
                      n++;
                  }

              // apply move towards average position
              Vec3d move = (sum / n - v.Position) * weight;
                  _moveSums[i] += move;
                  _weightSums[i] += weight;
              }
          });
    }


    /// <summary>
    ///
    /// </summary>
    /// <param name="weight"></param>
    /// <returns></returns>
    void BoundarySmooth(double weight)
    {
        System.Threading.Tasks.Parallel.For(0, _holes.Count, i =>
          {
              foreach (HalfEdge e in _holes[i].CirculateFace)
              {
                  HeVertex v0 = e.Start;
                  HeVertex v1 = e.End;
                  HeVertex v2 = e.Previous.Start;

                  int i0 = v0.Index;
                  int i1 = v1.Index;
                  int i2 = v2.Index;

              // apply move towards average position
              Vec3d move = ((v1.Position + v2.Position) * 0.5 - v0.Position) * weight;
                  _moveSums[i0] += move;
                  _weightSums[i0] += weight;

              // apply reverse move to neighbours
              move *= -0.5;
                  _moveSums[i1] += move;
                  _weightSums[i1] += weight;
                  _moveSums[i2] += move;
                  _weightSums[i2] += weight;
              }
          });
    }


    /// <summary>
    /// Calculates collision forces between vertices.
    /// </summary>
    /// <param name="weight"></param>
    /// <returns></returns>
    void Collide(double weight)
    {
        double rad2 = _radius * 2.0;
        double rad2sqr = rad2 * rad2;
        Vec3d offset = new Vec3d(rad2, rad2, rad2);

        // insert vertex positions into hash
        for (int i = 0; i < _verts.Count; i++)
            _grid.Insert(_verts[i].Position, i);

        // batch process collisions between particles
        var chunks = System.Collections.Concurrent.Partitioner.Create(0, _verts.Count);
        System.Threading.Tasks.Parallel.ForEach(chunks, range =>
          {
              for (int i = range.Item1; i < range.Item2; i++)
              {
                  HeVertex v = _verts[i];
                  Vec3d p = v.Position;

              // search grid and handle potential collisions
              _grid.Search(new Domain3d(p - offset, p + offset), foundIds =>
                {
                  foreach (int j in foundIds)
                  {
                      if (j == i) continue; // ignore self-collision

                  // This is more expensive than applying the reverse collision force between 1 ring neighbours in a separate method
                  // if(v.FindEdgeTo(_verts[j]) != null) continue; // ignore collisions between 1 ring neighbours

                  Vec3d move = _verts[j].Position - p;
                      double d = move.SquareLength;

                      if (d < rad2sqr && d > 0.0)
                      {
                          d = Math.Sqrt(d);
                          move *= (d - rad2) / d * weight; // *0.5;
                      _moveSums[i] += move;
                          _weightSums[i] += weight;
                      }
                  }
              });
              }
          });

        _grid.Clear();
    }


    /// <summary>
    /// Cancels out collision forces between 1 ring neighbours.
    /// </summary>
    /// <param name="weight"></param>
    /// <returns></returns>
    void CollideCancel(double weight)
    {
        double rad2 = _radius * 2.0;
        double rad2sqr = rad2 * rad2;

        var chunks = System.Collections.Concurrent.Partitioner.Create(0, _verts.Count);
        System.Threading.Tasks.Parallel.ForEach(chunks, range =>
          {
              for (int i = range.Item1; i < range.Item2; i++)
              {
                  HeVertex v = _verts[i];
                  Vec3d p = v.Position;

                  foreach (HeVertex cv in v.ConnectedVertices)
                  {
                      Vec3d move = cv.Position - p;
                      double d = move.SquareLength;

                      if (d < rad2sqr && d > 0.0)
                      {
                          d = Math.Sqrt(d);
                          move *= (d - rad2) / d * weight; // *0.5;
                      _moveSums[i] -= move;
                          _weightSums[i] += weight;
                      }
                  }
              }
          });
    }


    /// <summary>
    ///
    /// </summary>
    /// <param name="weight"></param>
    /// <returns></returns>
    void CollideBrute(double weight)
    {
        double rad2 = _radius * 2.0;
        double rad2sqr = rad2 * rad2;

        for (int i = 0; i < _verts.Count; i++)
        {
            HeVertex v = _verts[i];
            Vec3d p = v.Position;

            for (int j = i + 1; j < _verts.Count; j++)
            {
                if (v.FindHalfEdgeTo(_verts[j]) != null) continue; // ignore collisions between 1 ring neigbours

                Vec3d move = _verts[j].Position - p;
                double d = move.SquareLength;

                if (d < rad2sqr && d > 0.0)
                {
                    d = Math.Sqrt(d);
                    move *= (d - rad2) / d * weight; // *0.5;

                    _moveSums[i] += move;
                    _weightSums[i] += weight;

                    _moveSums[j] -= move;
                    _weightSums[j] += weight;
                }
            }
        }
    }


    /// <summary>
    ///
    /// </summary>
    /// <returns></returns>
    void UpdateVertices()
    {
        var chunks = System.Collections.Concurrent.Partitioner.Create(0, _verts.Count);
        System.Threading.Tasks.Parallel.ForEach(chunks, range =>
          {
              for (int i = range.Item1; i < range.Item2; i++)
              {
                  double w = _weightSums[i];
                  if (w > 0.0)
                      _velocities[i] += _moveSums[i] * (_timeStep / w);

                  _verts[i].Position += _velocities[i];
                  _velocities[i] *= _decay;
                  _moveSums[i] = Vec3d.Zero;
                  _weightSums[i] = 0.0;
              }
          });
    }


    /// <summary>
    /// Refines topology of mesh.
    /// Splits edges which have exceeded a maximum length (i.e. some factor of the collision radius).
    /// Spins edges to equalize vertex valence across the mesh.
    /// </summary>
    /// <returns></returns>
    void RefineMesh()
    {
        SplitEdges();
        EqualizeValence();
    }


    /// <summary>
    ///
    /// </summary>
    /// <returns></returns>
    void SplitEdges()
    {
        int nv = _verts.Count;
        int ne = _edges.Count;
        double max = _splitLength * _splitLength;

        // split edges that exceed the maximum length
        for (int i = 0; i < ne; i += 2)
        {
            HalfEdge e = _edges[i];
            if (e.IsUnused) continue;

            double d = e.Span.SquareLength;
            if (d > max)
            {
                _edges.SplitEdgeFace(e);
                _restLengths[i >> 1] = Math.Sqrt(d) * 0.5; // update the length of the edge that was split
            }
        }

        // add attributes for any new vertices that were created
        for (int i = nv; i < _verts.Count; i++)
        {
            _velocities.Add(Vec3d.Zero);
            _moveSums.Add(Vec3d.Zero);
            _weightSums.Add(0.0);
            _degrees.Add(0);
        }

        // add attributes for any new half-edge pairs that were created
        for (int i = ne; i < _edges.Count; i += 2)
        {
            _restLengths.Add(_edges[i].Length);
            _spinOrder.Add(i >> 1);
        }
    }


    /// <summary>
    /// Attempts to equalize the valence of vertices by spinning interior edges
    /// </summary>
    /// <returns></returns>
    void EqualizeValence()
    {
        _verts.UpdateVertexDegrees(_degrees);
        _spinOrder.Shuffle(_random); // shuffle the spin order to avoid bias

        foreach (int i in _spinOrder)
        {
            HalfEdge e = _edges[i << 1];
            if (e.IsUnused || e.IsBoundary) continue;

            HeVertex v0 = e.Start;
            HeVertex v1 = e.Previous.Start;
            e = e.Twin;
            HeVertex v2 = e.Start;
            HeVertex v3 = e.Previous.Start;

            int i0 = v0.Index;
            int i1 = v1.Index;
            int i2 = v2.Index;
            int i3 = v3.Index;

            // current valence error
            int t0 = _degrees[i0] - ((v0.IsBoundary) ? 4 : 6);
            int t1 = _degrees[i1] - ((v1.IsBoundary) ? 4 : 6);
            int t2 = _degrees[i2] - ((v2.IsBoundary) ? 4 : 6);
            int t3 = _degrees[i3] - ((v3.IsBoundary) ? 4 : 6);
            int error0 = t0 * t0 + t1 * t1 + t2 * t2 + t3 * t3;

            // flipped valence error
            t0--; t1++; t2--; t3++;
            int error1 = t0 * t0 + t1 * t1 + t2 * t2 + t3 * t3;

            // flip edge if it results in less error
            if (error1 < error0)
            {
                _edges.SpinEdge(e);
                _restLengths[i >> 1] = e.Length; // update length of edge

                // update vertex degrees
                _degrees[i0]--;
                _degrees[i1]++;
                _degrees[i2]--;
                _degrees[i3]++;
            }
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
        System.Object heMesh = default(System.Object);
        if (inputs[0] != null)
        {
            heMesh = (System.Object)(inputs[0]);
        }

        double edgeLength = default(double);
        if (inputs[1] != null)
        {
            edgeLength = (double)(inputs[1]);
        }

        List<double> weights = null;
        if (inputs[2] != null)
        {
            weights = GH_DirtyCaster.CastToList<double>(inputs[2]);
        }
        double growthRate = default(double);
        if (inputs[3] != null)
        {
            growthRate = (double)(inputs[3]);
        }

        int subSteps = default(int);
        if (inputs[4] != null)
        {
            subSteps = (int)(inputs[4]);
        }

        bool reset = default(bool);
        if (inputs[5] != null)
        {
            reset = (bool)(inputs[5]);
        }

        bool go = default(bool);
        if (inputs[6] != null)
        {
            go = (bool)(inputs[6]);
        }



        //3. Declare output parameters
        object result = null;


        //4. Invoke RunScript
        RunScript(heMesh, edgeLength, weights, growthRate, subSteps, reset, go, ref result);

        try
        {
            //5. Assign output parameters to component...
            if (result != null)
            {
                if (GH_Format.TreatAsCollection(result))
                {
                    IEnumerable __enum_result = (IEnumerable)(result);
                    DA.SetDataList(1, __enum_result);
                }
                else
                {
                    if (result is Grasshopper.Kernel.Data.IGH_DataTree)
                    {
                        //merge tree
                        DA.SetDataTree(1, (Grasshopper.Kernel.Data.IGH_DataTree)(result));
                    }
                    else
                    {
                        //assign direct
                        DA.SetData(1, result);
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