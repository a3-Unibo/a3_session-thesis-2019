![Symmetric Perlin Noise](https://raw.githubusercontent.com/a3-Unibo/a3_session-thesis-2019/master/%40%20media/symmNoise%2001.png)

# a3_session-thesis-2019
Codes used in the Thesis sessions 2019

Please note that this material is a compendium to in-person teaching workshop days, so many implied instructions, premises and cautions given during the dat-to-day development have not been included. - tutor: [Alessio Erioli](https://www.unibo.it/sitoweb/alessio.erioli/)

Tools used: Rhinoceros 3D v6 (includes Grasshopper), Visual Studio 2017.

---

## AgentSystemFinal

Contains .gh files, the Visual Studio project files and .sln file for the final version of the simulation. There are 3 .gh files: basic, intermediate and full-optional. Read the enclosed **README FIRST.txt** and **changelog.txt** files for full specifications.

## @ utilities

Contains .gha asssemblies and .dll libraries and general purpose .gh definitions used in the workshop.

**3Dpeople_20181116** - 3D people as meshes in 3 different resolutions

**M00_Millipede FEM field.gh** - simple use of Millipede Grasshopper plugin to generate a scalar and vector field of structural information over a FEM model of a mesh surface  
**M01_Millipede graphics generator.gh** - generates and bakes geometry for 3 different diagrams of Millipede generated data  
*Millipede_data.ghdata* - this file is a sample of how data is passed between M00 and M01  
**interpolate mesh data.gh** - interpolate scalar and vector data while performing a Catmull-Clark subdivision of a mesh - sometimes Millipede can be slow on big geometries. This definition allows the use of a lower-resolution mesh for faster analysis and interpolate data to use on a high-res mesh

**Util_Clipping plane - Turntable base.3dm**  
**Util-01_clipping plane anim.gh**  
**Util-02_turntable.gh**  
**Util-03_record animation.gh**  
These files are helpers to generate, respectively: an animation of a moving clipping plane (for a model tomography), a turntable of one or more geometries, an animation of the agents from their trajectories as polylines
  
**Util_post-processing-Dendro** - template for isosurfacing line-base network geometries. Reading the [Dendro](https://www.food4rhino.com/app/dendro) plugin documentation is strongly suggested here  
  
**base meshes.gh** - reference mesh models that can be use in exercises  
  
### @ utilities/Components
**FileToScript2.gha** - syncs the code of a C# or VB scripting component in Grasshopper with an external editor - this is an updated version for Rhino6 of FileToScript.gha, a tool written by Mateusz Zwierzycki wrapping up a code by Vicente Soler - additional code to update it for Rhino 6 by Daniel Fink, wrapped and recompiled as a .gha assembly by Alessio Erioli. [Original discussion on FileToScript](https://www.grasshopper3d.com/forum/topics/file-to-script-maths?groupUrl=milkbox&).

**Noises.zip** - library with Simplex Noise generation functions, it can be used to embed Noise calculations (including Curl Noise, which is based on Simplex Noise) in a custom C# script
<br>

### @ utilities/Display Modes
Contains a bunch of customized Display Modes for Rhino 6 - they can be installed in Rhino from:  
_Tools > Options > View > Display Modes > Import_
<br>

### @ utilities/Mesh Modeling
Rhino files and Grasshopper definitions for basic Mesh modeling (low poly to subdivision techniques)

---
## codes

These folders contain the codes, organized as follows:
  
**GH_<something>** - all things Grasshopper-focused: intuition and C# introductory codes  
**VS_Codes** - all codes developed for complex strategies with Visual Studio as IDE  
  
### GH_CSharp
This folder contains all the Grasshopper definitions with a progressive introduction to C#.
  
**CS_00_intro.gh** - introduction to C# programming in Grasshopper  
**CS_01_data 01.gh** - data types in C# - part 1  
**CS_01_data 02.gh** - data types in C# - part 2 - loops and conditional statements  
**CS_02_functions.gh** - functions in C#  
**CS_03_classes.gh** - classes and objects in C#  
**CS_04_gradient descent.gh** - gradient descent example in C#  
**CS_05_delegates example.gh** - explanation of delegates, anonymous functions and lambda syntax in C#  
**CS_06_RTree point search.gh** - using RTree data structure in C# - simple example of nearest neighbours search  
  
  
### GH_Intuition
This folder contains all the Grasshopper definitions with strategies developed with standard components and plugins for an intuitive comprehension before plunging into code writing.
  
**01-00_iterative strategies - intuition.gh** - introduction to iterative strategies in Grasshopper - intuitive approach (standard compopnents + Anemone plug-in)  
**01-01_environment and field - intuition.gh** - reading information from an environment/field - intuitive approach  
**01-02_boundary behaviors intuition.gh** - simple boundary behavior - intuitive approach  
**01-03_environment and field - wrap - intuition.gh** - boundary wrap behavior - intuitive approach  
**02-00_stigmergy - basic - intuition.gh** - reading and writing information in an environment - intuitive approach  

#### VS_Code/Jitterbug
Visual Studio project folder for the Jitterbug basic class example   
  
#### VS_Code/AgentSystemFlock
Visual Studio project folder for the basic Craig Reynolds Flocking Agent System  
  
#### VS_Code/AgentSystemFlockField
Visual Studio project folder for the basic Craig Reynolds Flocking Agent System + Field influence  
  
#### VS_Code/AgentSystemFinal
Visual Studio project folder for the evolved version of the Agent System - agents are capable of patrolling a mesh surface, read scalar and vector data and release elementary bodies along their trajectories whose formation results in a performative ornamentation

