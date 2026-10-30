using System.Collections;

namespace GameKit.UI
{
    public interface IShowable
    {
        void Show(bool playDefaultAnimation = false, bool makeStateinteractable = true );
        IEnumerator Hide(bool playDefaultAnimation = false);
        void Pause();
        void Resume();
    }
}
