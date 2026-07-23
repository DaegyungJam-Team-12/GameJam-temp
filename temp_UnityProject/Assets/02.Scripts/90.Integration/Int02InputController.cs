#nullable enable

using System;
using UnityEngine.InputSystem;

namespace Icebreaker.Integration
{
    /// <summary>
    /// INT-02 통합 씬에서 하드웨어 입력을 감지하고 이벤트를 방출하는 역할만 수행합니다. (SRP)
    /// </summary>
    public sealed class Int02InputController
    {
        /// <summary>
        /// 사용자가 ESC(뒤로가기/설정)를 요청했을 때 방출됩니다.
        /// </summary>
        public event Action EscapeRequested = delegate { };

        public void Tick()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                EscapeRequested();
            }
        }
    }
}
