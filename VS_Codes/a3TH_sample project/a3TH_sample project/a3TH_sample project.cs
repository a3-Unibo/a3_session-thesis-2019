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
    private void RunScript(bool reset, bool go, List<Point3d> P, ref object POut)
    {
        // <Custom code> 

        Point3d[] ptsOut = new Point3d[P.Count];

        // reset conditions
        if (reset || agSys == null)
        {
            agSys = new AgentSystem(P);
        }

        // component run and update
        if (go)
        {
            agSys.Update();
            Component.ExpireSolution(true);
        }

        // output values

        for (int i = 0; i < ptsOut.Length; i++) ptsOut[i] = agSys.Agents[i].position;

        POut = ptsOut;

        // </Custom code> 
    }

    // <Custom additional code> 

    // global variables
    public AgentSystem agSys;
    public static Random rnd;

    // simulation system class
    public class AgentSystem
    {
        // fields
        public List<Agent> Agents;

        // constructor(s)
        public AgentSystem(List<Point3d> pts)
        {
            Agents = new List<Agent>();
            rnd = new Random();

            foreach (Point3d p in pts)
            {
                Agent ag = new Agent(p, new Vector3d(rnd.NextDouble() - 0.5, rnd.NextDouble() - 0.5, 0));
                Agents.Add(ag);
            }
        }

        // methods
        public void Update()
        {
            foreach (Agent ag in Agents) ag.Move();
        }
    }

    // individual object class(es)
    public class Agent
    {
        // fields
        public Point3d position;
        public Vector3d velocity;

        // constructor(s)
        public Agent(Point3d position, Vector3d velocity)
        {
            this.position = position;
            this.velocity = velocity;
        }

        // methods
        public void Move()
        {
            position += velocity;

        }
    }

    // utility classs(es)

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

        List<Point3d> P = null;
        if (inputs[2] != null)
        {
            P = GH_DirtyCaster.CastToList<Point3d>(inputs[2]);
        }


        //3. Declare output parameters
        object A = null;


        //4. Invoke RunScript
        RunScript(reset, go, P, ref A);

        try
        {
            //5. Assign output parameters to component...
            if (A != null)
            {
                if (GH_Format.TreatAsCollection(A))
                {
                    IEnumerable __enum_A = (IEnumerable)(A);
                    DA.SetDataList(1, __enum_A);
                }
                else
                {
                    if (A is Grasshopper.Kernel.Data.IGH_DataTree)
                    {
                        //merge tree
                        DA.SetDataTree(1, (Grasshopper.Kernel.Data.IGH_DataTree)(A));
                    }
                    else
                    {
                        //assign direct
                        DA.SetData(1, A);
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