using System;
using System.Drawing;
using Grasshopper.Kernel;

namespace DifferentialGrowth_Reeves
{
    public class DifferentialGrowth_ReevesInfo : GH_AssemblyInfo
    {
        public override string Name
        {
            get
            {
                return "DifferentialGrowthReeves";
            }
        }
        public override Bitmap Icon
        {
            get
            {
                //Return a 24x24 pixel bitmap to represent this GHA library.
                return null;
            }
        }
        public override string Description
        {
            get
            {
                //Return a short string describing the purpose of this GHA library.
                return "";
            }
        }
        public override Guid Id
        {
            get
            {
                return new Guid("00329634-4c3a-40e0-be97-25dce0dd8780");
            }
        }

        public override string AuthorName
        {
            get
            {
                //Return a string identifying you or your company.
                return "";
            }
        }
        public override string AuthorContact
        {
            get
            {
                //Return a string representing your preferred contact details.
                return "";
            }
        }
    }
}
