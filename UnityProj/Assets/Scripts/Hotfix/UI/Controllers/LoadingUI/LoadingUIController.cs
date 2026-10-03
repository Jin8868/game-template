using System.Threading;
using AlloyFramework.UI;
using Cysharp.Threading.Tasks;

namespace Game.UI
{
    public sealed class LoadingUIController : UIController<LoadingUIView>
    {
        // Controller 和 View 首次创建后调用一次。
        protected override void OnCreate()
        {
        }

        // 每次打开前调用，可以异步准备数据和资源。
        protected override UniTask OnPrepareAsync(
            UIEmptyData data, CancellationToken cancellationToken)
        {
            return UniTask.CompletedTask;
        }

        // 每次打开时初始化界面数据。
        protected override void OnInitData(UIEmptyData data)
        {
            
        }

        // 已打开的界面以刷新模式再次打开时调用。
        protected override void OnRefresh(UIEmptyData data)
        {
        }

        // 开始播放打开动画前调用。
        protected override void OnStartOpenAnimation()
        {
        }

        // 返回打开动画任务；没有动画时直接返回已完成任务。
        protected override UniTask OnOpenAnimationAsync(
            CancellationToken cancellationToken)
        {
            return UniTask.CompletedTask;
        }

        // 打开动画执行完成后调用。
        protected override void OnEndOpenAnimation()
        {
        }

        // 界面完成打开并可以交互后调用。
        protected override void OnOpen()
        {
        }

        // 界面被更高层界面暂停时调用。
        protected override void OnPause()
        {
        }

        // 界面从暂停状态恢复时调用。
        protected override void OnResume()
        {
        }

        // 界面开始关闭时调用。
        protected override void OnClose()
        {
        }

        // 开始播放关闭动画前调用。
        protected override void OnStartCloseAnimation()
        {
        }

        // 返回关闭动画任务；没有动画时直接返回已完成任务。
        protected override UniTask OnCloseAnimationAsync()
        {
            return UniTask.CompletedTask;
        }

        // 关闭动画执行完成后调用。
        protected override void OnEndCloseAnimation()
        {
        }

        // Controller 被彻底释放时调用。
        protected override void OnDispose()
        {
        }
    }
}
