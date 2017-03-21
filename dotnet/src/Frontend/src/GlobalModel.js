let counter = 0;
const LOG_MAX = 100;

export default class GlobalModel {
  constructor() {
    Iris.startContext(info => {
      if (this.logSubscription == null) {
        this.logSubscription = Iris.subscribeToLogs(log => {
          this.addLog(log);
        })
      }
      this.__setState("pinGroups", info.state.PinGroups);
    });

    this.subscribers = new Map();
    this.eventSubscribers = new Map();
    this.state = {
      tabs: new Map(),
      widgets: new Map()
    };
  }

  subscribe(key, subscriber) {
    let id = counter++, subscribers = this.subscribers;
    if (!subscribers.has(key)) {
      subscribers.set(key, new Map());
    }
    subscribers.get(key).set(id, subscriber);
    // `subscribers` must be captured so the closure below works
    return {
      dispose() {
        subscribers.get(key).delete(id);
      }
    }
  }

  subscribeToEvent(event, subscriber) {
    let id = counter++, subscribers = this.eventSubscribers;
    if (!subscribers.has(event)) {
      subscribers.set(event, new Map());
    }
    subscribers.get(event).set(id, subscriber);
    // `subscribers` must be captured so the closure below works
    return {
      dispose() {
        subscribers.get(event).delete(id);
      }
    }
  }

  __notify(key, value = this.state[key]) {
    if (this.subscribers.has(key)) {
      this.subscribers.get(key).forEach(subscriber => subscriber(value));
    }
  }

  __setState(key, value) {
    this.state[key] = value;
    this.__notify(key, value);
  }

  addWidget(id, widget) {
    if (widget === void 0) {
      widget = id;
      id = counter++;
    }
    this.state.widgets.set(id, widget);
    this.__notify("widgets");
    return id;
  }

  removeWidget(id) {
    this.state.widgets.delete(id);
    this.__notify("widgets");
  }

  addTab(id, tab) {
    if (tab === void 0) {
      tab = id;
      id = counter++;
    }
    this.state.tabs.set(id, tab);
    this.__notify("tabs");
    return id;
  }

  removeTab(id) {
    this.state.tabs.delete(id);
    this.__notify("tabs");
  }

  addLog(log) {
    var logs = this.state.logs;
    if (Array.isArray(logs)) {
      if (logs.length > LOG_MAX) {
        var diff = Math.floor(LOG_MAX / 100);
        logs.splice(logs.length - diff, diff);
      }
      logs.splice(0, 0, [counter++, log]);
      this.__notify("logs", logs);
    }
  }

  triggerEvent(event, data) {
    if (this.eventSubscribers.has(event)) {
      this.eventSubscribers.get(event).forEach(subscriber => subscriber(data));
    }
  }
}