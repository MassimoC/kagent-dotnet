from flask import Flask, jsonify, request
from datetime import datetime
import json

app = Flask(__name__)

LOG_FILE = "requests.log"


def log_request():
    timestamp = datetime.utcnow().isoformat() + "Z"

    try:
        body = request.get_json(silent=True)
    except Exception:
        body = None

    log_entry = {
        "timestamp": timestamp,
        "method": request.method,
        "path": request.path,
        "headers": dict(request.headers),
        "body": body,
        "raw_body": request.get_data(as_text=True)
    }

    with open(LOG_FILE, "a", encoding="utf-8") as f:
        f.write(json.dumps(log_entry, ensure_ascii=False) + "\n")


@app.before_request
def before_request():
    log_request()


@app.route("/api/sessions", methods=["GET", "POST"])
def sessions_root():
    return jsonify({}), 200


@app.route("/api/sessions/<path:x>", methods=["GET", "POST", "PUT", "DELETE"])
def session_route(x):
    return jsonify({}), 200


@app.route("/api/tasks", methods=["GET", "POST"])
def tasks_root():
    return jsonify({}), 200


@app.route("/api/tasks/<path:x>", methods=["GET", "POST", "PUT", "DELETE"])
def task_route(x):
    return jsonify({}), 200


@app.errorhandler(404)
def not_found(e):
    return jsonify({}), 404


if __name__ == "__main__":
    app.run(host="0.0.0.0", port=3000)
