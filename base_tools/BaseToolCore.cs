using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using g3;
using f3;

namespace gs
{

    /// <summary>
    /// Useful functionality in many tools for:
    ///    - controlling AllowSelectionChanges
    ///    - managing tool-internal history stream
    /// </summary>
    public class BaseToolCore
    {
        public FScene Scene;


        // shutdown bits
        bool is_shutting_down = false;
        protected virtual void begin_shutdown() {
            is_shutting_down = true;
        }
        protected virtual bool in_shutdown() {
            return is_shutting_down;
        }



        //
        // selection-changes control
        //

        public virtual bool AllowSelectionChanges { get { return allow_selection_changes; } }
        bool allow_selection_changes = false;
        protected virtual void set_allow_selection_changes(bool allow)
        {
            allow_selection_changes = allow;
        }



        //
        // support for in-tool history stream
        //

        protected virtual bool enable_internal_history_stream() { return true; }

        bool pushed_history_stream = false;

        protected virtual void push_history_stream()
        {
            if (enable_internal_history_stream() == false)
                return;

            Util.gDevAssert(pushed_history_stream == false);
            if (!pushed_history_stream) {
                Scene.PushHistoryStream();
                pushed_history_stream = true;
            }
        }


        protected virtual void pop_history_stream()
        {
            if (enable_internal_history_stream() == false)
                return;

            if (pushed_history_stream) {
                Scene.PopHistoryStream();
                pushed_history_stream = false;
            }
        }

    }
}
