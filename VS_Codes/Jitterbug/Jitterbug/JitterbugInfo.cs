using System;
using System.Drawing;
using Grasshopper.Kernel;

namespace Jitterbug
{
    public class JitterbugInfo : GH_AssemblyInfo
    {
        public override string Name
        {
            get
            {
                return "Jitterbug";
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
                return new Guid("353ea781-85bf-4df0-bd46-eb8b48b4d163");
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
