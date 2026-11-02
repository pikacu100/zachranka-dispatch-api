// Dispatcher board: plain JS, talks to the REST API and listens to the SignalR hub.
"use strict";

const PRIORITY_LABELS = { Critical: "Kritická", High: "Vysoká", Medium: "Střední", Low: "Nízká" };
const UNIT_STATUS_LABELS = { Available: "Volná", Dispatched: "Vyslaná", OnScene: "Na místě", OutOfService: "Mimo provoz" };
const UNIT_TYPE_LABELS = { Ambulance: "ZZS", Fire: "HZS", Police: "PČR" };
const CLOSED_SHOWN = 15;

const state = { incidents: [], units: [], priority: "", flashIds: new Set() };

// --- API ----------------------------------------------------------------------------------------

async function api(method, path, body) {
    const response = await fetch(path, {
        method,
        headers: body ? { "Content-Type": "application/json" } : undefined,
        body: body ? JSON.stringify(body) : undefined,
    });
    if (!response.ok) {
        // The API returns RFC 7807 ProblemDetails; show the most specific message it has.
        let message = `${response.status} ${response.statusText}`;
        try {
            const problem = await response.json();
            const validation = problem.errors ? Object.values(problem.errors).flat().join(" ") : "";
            message = problem.detail || validation || problem.title || message;
        } catch { /* not JSON */ }
        throw new Error(message);
    }
    return response.status === 204 ? null : response.json();
}

async function load() {
    const query = state.priority ? `?priority=${encodeURIComponent(state.priority)}` : "";
    const [incidents, units] = await Promise.all([
        api("GET", `api/incidents${query}`),
        api("GET", "api/units"),
    ]);
    state.incidents = incidents;
    state.units = units;
    render();
}

// Several SignalR events can arrive at once (e.g. seeding); reload once for the whole burst.
let reloadTimer;
function scheduleReload() {
    clearTimeout(reloadTimer);
    reloadTimer = setTimeout(() => load().catch(showError), 150);
}

// --- Rendering ----------------------------------------------------------------------------------

const escapeHtml = (text) =>
    String(text ?? "").replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" })[c]);

const formatTime = (iso) =>
    new Date(iso).toLocaleString("cs-CZ", { day: "numeric", month: "numeric", hour: "2-digit", minute: "2-digit" });

function render() {
    const available = state.units.filter((u) => u.status === "Available");

    for (const column of document.querySelectorAll(".column")) {
        const status = column.dataset.status;
        let incidents = state.incidents.filter((i) => i.status === status);
        const total = incidents.length;
        if (status === "Closed") incidents = incidents.slice(0, CLOSED_SHOWN);

        column.querySelector(".count").textContent = status === "Closed" && total > CLOSED_SHOWN
            ? `${CLOSED_SHOWN} z ${total}`
            : total;
        column.querySelector(".cards").innerHTML = incidents.length
            ? incidents.map((i) => renderCard(i, available)).join("")
            : `<div class="empty">Nic tu není</div>`;
    }

    document.getElementById("units").innerHTML = state.units.map((u) => `
        <li>
            <span>${escapeHtml(u.callSign)} <span class="muted">${UNIT_TYPE_LABELS[u.type] ?? u.type}</span></span>
            <span class="unit-status ${u.status}">${UNIT_STATUS_LABELS[u.status] ?? u.status}</span>
        </li>`).join("");
    document.getElementById("units-summary").textContent = `${available.length} volných z ${state.units.length}`;

    state.flashIds.clear();
}

function renderCard(incident, availableUnits) {
    const chips = incident.assignments.map((a) =>
        `<span class="chip ${a.releasedAt ? "released" : ""}">${escapeHtml(a.callSign)}</span>`).join("");

    const actions = [];
    if (incident.status !== "Closed") {
        const options = availableUnits.map((u) =>
            `<option value="${u.id}">${escapeHtml(u.callSign)}</option>`).join("");
        actions.push(availableUnits.length
            ? `<select data-unit-select aria-label="Volná jednotka">${options}</select><button data-action="assign">Vyslat</button>`
            : `<span class="muted">Žádná volná jednotka</span>`);
    }
    if (incident.status === "Dispatched") actions.push(`<button data-action="status" data-status="OnScene">Na místě</button>`);
    if (incident.status === "New") actions.push(`<button class="secondary" data-action="status" data-status="Closed">Planý poplach</button>`);
    if (incident.status === "Dispatched" || incident.status === "OnScene") {
        actions.push(`<button class="secondary" data-action="status" data-status="Closed">Uzavřít</button>`);
    }

    return `
        <article class="card ${state.flashIds.has(incident.id) ? "flash" : ""}" data-id="${incident.id}" data-priority="${incident.priority}">
            <div class="card-head">
                <span class="card-title">${escapeHtml(incident.title)}</span>
                <span class="badge">${PRIORITY_LABELS[incident.priority] ?? incident.priority}</span>
            </div>
            <div class="card-meta">#${incident.id} · ${escapeHtml(incident.address)} · ${formatTime(incident.createdAt)}</div>
            ${incident.description ? `<p class="card-desc">${escapeHtml(incident.description)}</p>` : ""}
            ${chips ? `<div class="chips">${chips}</div>` : ""}
            ${actions.length ? `<div class="actions">${actions.join("")}</div>` : ""}
        </article>`;
}

// --- UI events ----------------------------------------------------------------------------------

let toastTimer;
function showToast(message, isError = false) {
    const toast = document.getElementById("toast");
    toast.textContent = message;
    toast.className = `toast ${isError ? "error" : ""}`;
    toast.hidden = false;
    clearTimeout(toastTimer);
    toastTimer = setTimeout(() => (toast.hidden = true), 4000);
}
const showError = (error) => showToast(error.message, true);

document.querySelector(".board").addEventListener("click", async (event) => {
    const button = event.target.closest("button[data-action]");
    if (!button) return;
    const card = button.closest(".card");
    const id = card.dataset.id;

    button.disabled = true;
    try {
        if (button.dataset.action === "assign") {
            const unitId = Number(card.querySelector("[data-unit-select]").value);
            await api("POST", `api/incidents/${id}/assignments`, { unitId });
        } else {
            await api("PUT", `api/incidents/${id}/status`, { status: button.dataset.status });
        }
        // No manual reload: the SignalR event refreshes this screen and every other one.
    } catch (error) {
        showError(error);
        button.disabled = false;
    }
});

document.getElementById("incident-form").addEventListener("submit", async (event) => {
    event.preventDefault();
    const form = event.target;
    const data = Object.fromEntries(new FormData(form));
    try {
        const created = await api("POST", "api/incidents", {
            title: data.title,
            address: data.address,
            description: data.description || null,
            priority: data.priority,
        });
        form.reset();
        showToast(`Výjezd #${created.id} založen`);
    } catch (error) {
        showError(error);
    }
});

document.getElementById("priority-filter").addEventListener("change", (event) => {
    state.priority = event.target.value;
    load().catch(showError);
});

// --- SignalR ------------------------------------------------------------------------------------

function setConnection(status, text) {
    const badge = document.getElementById("connection");
    badge.className = `connection ${status}`;
    badge.textContent = text;
}

const connection = new signalR.HubConnectionBuilder()
    .withUrl("hubs/dispatch")
    .withAutomaticReconnect()
    .build();

for (const eventName of ["IncidentCreated", "IncidentUpdated"]) {
    connection.on(eventName, (incident) => {
        state.flashIds.add(incident.id);
        scheduleReload();
    });
}
connection.onreconnecting(() => setConnection("connecting", "Obnovuji spojení…"));
connection.onreconnected(() => { setConnection("live", "Živě"); scheduleReload(); });
connection.onclose(() => setConnection("offline", "Offline"));

async function start() {
    await load().catch(showError);
    try {
        await connection.start();
        setConnection("live", "Živě");
    } catch {
        setConnection("offline", "Offline – obnov stránku");
    }
}

start();
