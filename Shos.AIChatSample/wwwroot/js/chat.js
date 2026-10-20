// チャット画面 (Pages/Index.cshtml) のクライアント側の処理
// - SignalR でサーバーのハブ (Hubs/ChatHub.cs) に接続し、メッセージの送受信を行う
// - 受信したメッセージは、サーバーでレンダリングした HTML (Messages/RenderPartial) を取得して表示する
//
// サーバーとのやり取り:
//   クライアント → サーバー: SendMessage(message) / Clear()
//   サーバー → クライアント: ReceiveMessage(id, user, message, createdAt) / Cleared()
"use strict";

document.addEventListener('DOMContentLoaded', run);

// 画面の初期化 (DOM の読み込み完了時に 1 回だけ呼ばれる)
function run() {
    // 切断されても自動で再接続する
    const connection   = new signalR.HubConnectionBuilder().withUrl("/chatHub").withAutomaticReconnect().build();
    const sendButton   = document.getElementById("sendButton"  );
    const clearButton  = document.getElementById("clearButton" );
    const messageInput = document.getElementById("messageInput");

    // 受信したメッセージを受信順に表示するためのキュー
    // (部分ビューの取得は非同期なので、そのまま並行に処理すると表示順が入れ替わることがある)
    // - Promise をつなげていくことで、前の処理が終わってから次の処理を始める
    // - 途中で失敗してもキューが止まらないよう、catch でエラーを握りつぶす
    let renderQueue = Promise.resolve();
    const enqueue = task => renderQueue = renderQueue.then(task).catch(err => console.error(err));

    // 接続が確立するまで、送信ボタンとクリアボタンを無効にする
    setEnabled(false);

    // サーバーからの通知を受け取るハンドラーを登録
    connection.on("ReceiveMessage", (id, user, message, createdAt) => enqueue(() => addMessagePartial(id, user, message, createdAt)));
    connection.on("Cleared"       , ()                              => enqueue(clearMessages));
    // 再接続中はボタンを無効にし、再接続できたら有効に戻す
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

    // サーバーでレンダリング済みのメッセージ (初期表示分) の時刻を変換し、最新のメッセージまでスクロールする
    toLocalTimeStringAll();
    scrollToBottom();

    // 入力欄のメッセージをサーバーに送信する
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
            // 送信に失敗したら入力内容を戻す (その間に新しい入力があれば上書きしない)
            if (messageInput.value === "")
                messageInput.value = message;
        }
    }

    // 送信ボタンとクリアボタンの有効 / 無効を切り替える
    function setEnabled(enabled) {
        sendButton .disabled = !enabled;
        clearButton.disabled = !enabled;
    }
}

// メッセージ一覧の要素を返す
function getMessagesList() {
    return document.getElementById('messagesList');
}

/**
 * 受信したメッセージを、サーバーでレンダリングした HTML で一覧に追加する
 * 取得に失敗した場合は、クライアント側で組み立てて追加する
 * @param {number} id        メッセージの ID
 * @param {string} user      送信者名
 * @param {string} message   本文
 * @param {string} createdAt 送信日時 (UTC の ISO-8601 文字列)
 */
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
            // サーバーでエンコード済みの HTML なので、innerHTML で DOM に変換してよい
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
// - 自分の発言かどうかはサーバー側でしか判定しないので、常に左側に表示する
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

    // クラス名とテキストを指定して要素を作る
    function createElement(tagName, className, text) {
        const element       = document.createElement(tagName);
        element.className   = className;
        element.textContent = text;
        return element;
    }
}

// メッセージ一覧を空にする
function clearMessages() {
    getMessagesList()?.replaceChildren();
}

// 未変換の UTC 時刻だけをローカル時刻に変換する (変換済みのものを再変換しない)
// - 元の UTC 時刻は datetime 属性に残し、変換済みの要素には data-converted 属性を付ける
// - data-converted が付くまでは CSS (site.css) で非表示にしている
function toLocalTimeStringAll() {
    document.querySelectorAll('.utc-time:not([data-converted])').forEach(element => {
        element.textContent       = toLocalTimeString(element.getAttribute('datetime'));
        element.dataset.converted = "true";
    });
}

// UTC の ISO-8601 文字列を、ブラウザーのロケールのローカル時刻の文字列に変換する
function toLocalTimeString(utcDateString) {
    return new Date(utcDateString).toLocaleString();
}

// メッセージ一覧を一番下 (最新のメッセージ) までスクロールする
function scrollToBottom() {
    const container = getMessagesList();
    if (container)
        container.scrollTop = container.scrollHeight;
}
