// API client: same-origin calls through the nginx proxy, JWT in Authorization header.
const KEY = "ona_session";

export class ApiError extends Error {}

export const session = {
  read() {
    try { return JSON.parse(localStorage.getItem(KEY)) || null; } catch { return null; }
  },
  write(value) {
    try { value ? localStorage.setItem(KEY, JSON.stringify(value)) : localStorage.removeItem(KEY); } catch { /* storage unavailable */ }
  },
};

/** Reads the role claim from the JWT (display only – the APIs enforce roles). */
export function roleOf(token) {
  try {
    const payload = token.split(".")[1].replace(/-/g, "+").replace(/_/g, "/");
    return JSON.parse(atob(payload)).role || "User";
  } catch {
    return "User";
  }
}

export async function call(method, path, { json, form } = {}) {
  const current = session.read();
  const headers = {};
  if (current?.token) headers.Authorization = `Bearer ${current.token}`;
  let body;
  if (json !== undefined) { headers["Content-Type"] = "application/json"; body = JSON.stringify(json); }
  if (form) body = form;

  let response;
  try {
    response = await fetch(path, { method, headers, body });
  } catch {
    throw new ApiError("Cannot reach the API. Is the stack running?");
  }
  if (response.status === 204) return null;
  let data = null;
  try { data = await response.json(); } catch { /* empty or non-JSON body */ }

  if (response.status === 401 && current?.token && !data?.code) {   // a 401 with a domain `code` (e.g. wrong current password) is not an expired session
    session.write(null);
    window.dispatchEvent(new Event("ona:signout"));
    throw new ApiError("Your session expired. Please sign in again.");
  }
  if (response.status === 403) throw new ApiError("You do not have permission to do that.");
  if (!response.ok) throw new ApiError(data?.detail || data?.title || `Request failed (${response.status})`);
  return data;
}
