// Live updates over SignalR. The browser client (vendor/signalr.min.js) exposes the global `signalR`.
// Two hubs, both behind the nginx proxy: /hubs/analysis and /hubs/insights. The JWT travels in the
// `access_token` query parameter because browsers cannot set headers on a WebSocket.
// Events are signals only ("something changed on this board"); the data itself is re-read over the HTTP API.
import { session } from "./api.js";

const RETRY_MS = [0, 2000, 5000, 10000, 30000];   // automatic reconnect back-off, then we keep trying every 30 s

export class Live {
  /**
   * @param {object} handlers
   * @param {(m:{analysis_run_id:string, board_id:string, bottleneck_count:number}) => void} handlers.analysisCompleted
   * @param {(m:{analysis_run_id:string, board_id:string, count:number}) => void} handlers.recommendationsGenerated
   * @param {(m:{recommendation_id:string, board_id:string, status:string}) => void} handlers.recommendationResponded
   * @param {(status:"live"|"connecting"|"offline") => void} handlers.status
   * @param {() => void} handlers.resync  called after a reconnect: events may have been missed while offline
   */
  constructor(handlers) {
    this.handlers = handlers;
    this.hubs = [];
    this.boardId = null;
    this.status = "offline";
    this.running = false;
  }

  start() {
    if (this.running) return;
    this.running = true;
    if (!window.signalR) { this.#setStatus("offline"); return; }   // script missing: the app falls back to polling
    this.hubs = [
      this.#hub("/hubs/analysis", { "analysis-run-completed": (m) => this.handlers.analysisCompleted(m) }),
      this.#hub("/hubs/insights", {
        "recommendations-generated": (m) => this.handlers.recommendationsGenerated(m),
        "recommendation-responded": (m) => this.handlers.recommendationResponded(m),
      }),
    ];
    this.#setStatus("connecting");
    for (const hub of this.hubs) this.#connect(hub);
  }

  stop() {
    this.running = false;
    const hubs = this.hubs;
    this.hubs = [];
    for (const hub of hubs) { clearTimeout(hub.timer); hub.connection.stop().catch(() => {}); }
    this.#setStatus("offline");
  }

  /** Subscribe to one board. Remembered, so it is repeated after every reconnect. */
  joinBoard(boardId) {
    if (boardId === this.boardId) return;
    const previous = this.boardId;
    this.boardId = boardId;
    for (const hub of this.hubs) {
      if (hub.connection.state !== signalR.HubConnectionState.Connected) continue;
      if (previous) hub.connection.invoke("LeaveBoardGroup", previous).catch(() => {});
      if (boardId) hub.connection.invoke("JoinBoardGroup", boardId).catch(() => {});
    }
  }

  #hub(path, events) {
    const connection = new signalR.HubConnectionBuilder()
      .withUrl(path, { accessTokenFactory: () => session.read()?.token ?? "" })
      .withAutomaticReconnect(RETRY_MS)
      .configureLogging(signalR.LogLevel.Warning)
      .build();
    for (const [name, handler] of Object.entries(events)) connection.on(name, handler);
    const hub = { connection, path };
    connection.onreconnecting(() => this.#refreshStatus());
    connection.onreconnected(async () => {
      await this.#subscribe(hub);
      this.#refreshStatus();
      this.handlers.resync();
    });
    connection.onclose(() => {
      this.#refreshStatus();
      if (this.running) hub.timer = setTimeout(() => this.#connect(hub), 30000);
    });
    return hub;
  }

  async #connect(hub) {
    if (!this.running) return;
    try {
      await hub.connection.start();
      await this.#subscribe(hub);
      this.#refreshStatus();
      if (this.hubs.every((h) => h.connection.state === signalR.HubConnectionState.Connected)) this.handlers.resync();
    } catch {
      this.#refreshStatus();
      if (this.running) hub.timer = setTimeout(() => this.#connect(hub), 5000);
    }
  }

  async #subscribe(hub) {
    if (this.boardId) await hub.connection.invoke("JoinBoardGroup", this.boardId).catch(() => {});
  }

  #refreshStatus() {
    const states = this.hubs.map((h) => h.connection.state);
    const S = signalR.HubConnectionState;
    this.#setStatus(
      states.length && states.every((s) => s === S.Connected) ? "live"
        : states.some((s) => s === S.Connecting || s === S.Reconnecting) ? "connecting" : "offline");
  }

  #setStatus(status) {
    if (status === this.status) return;
    this.status = status;
    this.handlers.status(status);
  }
}
