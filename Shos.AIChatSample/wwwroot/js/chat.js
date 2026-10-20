"use strict";

document.addEventListener('DOMContentLoaded', run);

function run() {
    const connection   = new signalR.HubConnectionBuilder().withUrl("/chatHub").withAutomaticReconnect().build();
    const sendButton   = document.getElementById("sendButton"  );
    const clearButton  = document.getElementById("clearButton" );
    const messageInput = document.getElementById("messageInput");

    // 受信したメッセージを受信順に表示するためのキュー
    // (部分ビューの取得は非同期なので、そのまま並行に処理すると表示順が入れ替わることがある)
    let renderQueue = Promise.resolve();
    const enqueue = task => renderQueue = renderQueue.then(task).catch(err => console.error(err));

    //Disable the send and clear button until connection is established.
    setEnabled(false);

    connection.on("ReceiveMessage", (id, user, message, createdAt) => enqueue(() => addMessagePartial(id, user, message, createdAt)));
    connection.on("Cleared"       , ()                              => enqueue(clearMessages));
    connection.onreconnecting(() => setEnabled(false));
    connection.onreconnected (() => setEnabled(true ));

    connection.start()
              .then (() => setEnabled(true))
              .catch(err => console.error(err.toString()));

    sendButton.addEventListener("click", event => {
        event.preventDefault();
        sendMessage();
    });

    messageInput.addEventListener("keydown", event => {
        // IME の変換確定の Enter では送信しない
        if (event.key === "Enter" && !event.isComposing) {
            event.preventDefault();
            sendMessage();
        }
    });

    clearButton.addEventListener("click", async event => {
        event.preventDefault();
        // 画面のクリアはサーバーからの "Cleared" 通知で全クライアント一斉に行う
        try {
            await connection.invoke("Clear");
        } catch (err) {
            console.error(err.toString());
        }
    });

    toLocalTimeStringAll();
    scrollToBottom();

    async function sendMessage() {
        const message = messageInput.value;
        if (message.trim() === "")
            return;

        messageInput.value = "";
        try {
            // サーバー側は AI の応答まで待つので、ここでは完了を待たずに次の入力を受け付ける
            await connection.invoke("SendMessage", message);
        } catch (err) {
            console.error(err.toString());
            // 送信に失敗したら入力内容を戻す
            if (messageInput.value === "")
                messageInput.value = message;
        }
    }

    function setEnabled(enabled) {
        sendButton .disabled = !enabled;
        clearButton.disabled = !enabled;
    }
}

function getMessagesList() {
    return document.getElementById('messagesList');
}

async function addMessagePartial(id, user, message, createdAt) {
    const container = getMessagesList();
    if (!container) return;

    try {
        // サーバーで部分ビューをレンダリングして取得
        const res = await fetch(`/Messages/RenderPartial?id=${encodeURIComponent(id)}`, { method: 'GET', headers: { 'X-Requested-With': 'XMLHttpRequest' } });
        if (!res.ok) {
            console.error('Failed to fetch partial:', res.status, res.statusText);
            // フォールバック: クライアント側組み立て
            addMessageRow(container, user, message, createdAt);
        } else {
            const temp = document.createElement('div');
            temp.innerHTML = await res.text();
            // 返ってくるのはルート要素が複数の可能性があるので、子ノードを順に追加
            while (temp.firstChild)
                container.appendChild(temp.firstChild);
        }
    } catch (e) {
        console.error('Error fetching partial:', e);
        // フォールバック: クライアント側組み立て
        addMessageRow(container, user, message, createdAt);
    }

    // 取得した断片に含まれる UTC 時刻をローカル表示へ
    toLocalTimeStringAll();
    scrollToBottom();
}

// _MessagePartial.cshtml と同じ構造をクライアント側で組み立てる (左側表示)
// - 値はすべて textContent で設定する (XSS 対策)
function addMessageRow(container, user, message, createdAt) {
    const div     = document.createElement("div");
    div.className = "talk talk_left";

    const p       = document.createElement("p");
    p.appendChild(createElement("span", "talk_user"   , user   ));
    p.appendChild(document.createTextNode(" "));
    p.appendChild(createElement("span", "talk_content", message));
    p.appendChild(document.createTextNode(" "));

    const time    = createElement("time", "utc-time", createdAt);
    time.setAttribute("datetime", createdAt);
    p.appendChild(time);

    div.appendChild(p);
    container.appendChild(div);

    function createElement(tagName, className, text) {
        const element       = document.createElement(tagName);
        element.className   = className;
        element.textContent = text;
        return element;
    }
}

function clearMessages() {
    getMessagesList()?.replaceChildren();
}

// 未変換の UTC 時刻だけをローカル時刻に変換する (変換済みのものを再変換しない)
function toLocalTimeStringAll() {
    document.querySelectorAll('.utc-time:not([data-converted])').forEach(element => {
        element.textContent       = toLocalTimeString(element.getAttribute('datetime'));
        element.dataset.converted = "true";
    });
}

function toLocalTimeString(utcDateString) {
    return new Date(utcDateString).toLocaleString();
}

function scrollToBottom() {
    const container = getMessagesList();
    if (container)
        container.scrollTop = container.scrollHeight;
}
