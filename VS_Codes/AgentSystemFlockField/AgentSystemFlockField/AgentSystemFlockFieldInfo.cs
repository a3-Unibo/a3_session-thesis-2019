using System;
using System.Drawing;
using Grasshopper.Kernel;

namespace AgentSystemFlockField
{
    public class AgentSystemFlockFieldInfo : GH_AssemblyInfo
    {
        public override string Name
        {
            get
            {
                return "AgentSystemFlockField";
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
                return new Guid("478c1271-9c6f-4cbe-be32-af171d7aa67d");
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
