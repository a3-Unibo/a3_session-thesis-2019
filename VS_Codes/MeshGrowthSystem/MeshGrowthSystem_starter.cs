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
  private void RunScript(bool reset, bool go, bool grow, bool useRTree, Mesh M, int SubIterationCount, int MaxVertexCount, Line MouseLine, Point3d Attractor, Line Obstacles, double EdgeLengthWeight, double CollisionDistance, double CollisionWeight, double BendingReistanceWeight, ref object oMesh)
  {
        // <Custom code>
        // token code
        // been in VS!
        // </Custom code>
  }

  // <Custom additional code> 
  
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
    this. doc = this.RhinoDocument;

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