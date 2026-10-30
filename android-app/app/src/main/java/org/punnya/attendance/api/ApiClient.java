package org.punnya.attendance.api;

import android.os.Handler;
import android.os.Looper;

import org.json.JSONArray;
import org.json.JSONObject;

import java.io.BufferedReader;
import java.io.InputStream;
import java.io.InputStreamReader;
import java.io.OutputStream;
import java.net.HttpURLConnection;
import java.net.URL;
import java.nio.charset.StandardCharsets;
import java.time.Instant;
import java.util.ArrayList;
import java.util.List;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

public class ApiClient {
    private final String baseUrl;
    private final ExecutorService executor = Executors.newCachedThreadPool();
    private final Handler mainHandler = new Handler(Looper.getMainLooper());

    public ApiClient(String baseUrl) {
        this.baseUrl = baseUrl.endsWith("/") ? baseUrl.substring(0, baseUrl.length() - 1) : baseUrl;
    }

    public void login(String email, String password, Models.ApiCallback<Models.LoginResult> callback) {
        executor.execute(() -> {
            try {
                JSONObject payload = new JSONObject();
                payload.put("email", email.trim());
                payload.put("password", password.trim());

                HttpResult res = makeRequest("POST", "/api/v1/auth/login", null, payload.toString());
                if (res.statusCode >= 200 && res.statusCode < 300) {
                    JSONObject json = new JSONObject(res.body);
                    Models.LoginResult r = new Models.LoginResult();
                    r.success = true;
                    r.token = json.optString("token");
                    JSONObject user = json.optJSONObject("user");
                    if (user != null) {
                        r.userId = user.optString("id");
                        r.email = user.optString("email");
                        r.fullName = user.optString("fullName");
                        r.role = user.optString("role");
                    }
                    postSuccess(callback, r);
                } else {
                    String err = parseErrorMessage(res.body, "Login failed. Invalid credentials.");
                    postError(callback, err);
                }
            } catch (Exception ex) {
                postError(callback, "Network error: " + ex.getMessage());
            }
        });
    }

    public void getCurrentShift(String token, Models.ApiCallback<Models.ShiftInfo> callback) {
        executor.execute(() -> {
            try {
                HttpResult res = makeRequest("GET", "/api/v1/shifts/current", token, null);
                if (res.statusCode >= 200 && res.statusCode < 300) {
                    JSONObject json = new JSONObject(res.body);
                    Models.ShiftInfo s = new Models.ShiftInfo();
                    s.status = json.optString("status", "NOT_CLOCKED_IN");
                    s.isClockedIn = "ACTIVE".equalsIgnoreCase(s.status);
                    s.totalHoursToday = json.optDouble("totalHoursToday", 0.0);
                    s.totalTimeFormatted = json.optString("totalTimeFormatted", "0h 0m");
                    s.activeTimeFormatted = json.optString("activeTimeFormatted", "0h 0m");
                    s.stationId = json.optString("stationId", "");

                    String clockInStr = json.optString("clockIn", null);
                    if (clockInStr != null && !clockInStr.isEmpty()) {
                        try {
                            s.clockInEpoch = Instant.parse(clockInStr).toEpochMilli();
                        } catch (Exception ignored) {
                            s.clockInEpoch = System.currentTimeMillis();
                        }
                    }
                    postSuccess(callback, s);
                } else {
                    postError(callback, "Failed to load shift status: HTTP " + res.statusCode);
                }
            } catch (Exception ex) {
                postError(callback, "Error: " + ex.getMessage());
            }
        });
    }

    public void clockIn(String token, String stationId, Models.ApiCallback<Models.ShiftInfo> callback) {
        executor.execute(() -> {
            try {
                JSONObject payload = new JSONObject();
                payload.put("stationId", stationId != null && !stationId.isEmpty() ? stationId : "MOBILE-WIFI");

                HttpResult res = makeRequest("POST", "/api/v1/shifts/clock-in", token, payload.toString());
                if (res.statusCode >= 200 && res.statusCode < 300) {
                    JSONObject json = new JSONObject(res.body);
                    Models.ShiftInfo s = new Models.ShiftInfo();
                    s.isClockedIn = true;
                    s.status = "ACTIVE";
                    s.stationId = json.optString("stationId", stationId);
                    s.totalHoursToday = json.optDouble("totalHoursToday", 0.0);
                    s.totalTimeFormatted = json.optString("totalTimeFormatted", "0h 0m");
                    s.clockInEpoch = System.currentTimeMillis();
                    postSuccess(callback, s);
                } else {
                    postError(callback, parseErrorMessage(res.body, "Failed to clock in"));
                }
            } catch (Exception ex) {
                postError(callback, "Network error clocking in: " + ex.getMessage());
            }
        });
    }

    public void clockOut(String token, String stationId, Models.ApiCallback<Models.ShiftInfo> callback) {
        executor.execute(() -> {
            try {
                JSONObject payload = new JSONObject();
                payload.put("stationId", stationId != null && !stationId.isEmpty() ? stationId : "MOBILE-WIFI");

                HttpResult res = makeRequest("POST", "/api/v1/shifts/clock-out", token, payload.toString());
                if (res.statusCode >= 200 && res.statusCode < 300) {
                    JSONObject json = new JSONObject(res.body);
                    Models.ShiftInfo s = new Models.ShiftInfo();
                    s.isClockedIn = false;
                    s.status = "COMPLETED";
                    s.totalHoursToday = json.optDouble("totalHoursToday", 0.0);
                    s.totalTimeFormatted = json.optString("totalTimeFormatted", "0h 0m");
                    s.activeTimeFormatted = json.optString("activeTimeFormatted", "0h 0m");
                    postSuccess(callback, s);
                } else {
                    postError(callback, parseErrorMessage(res.body, "Failed to clock out"));
                }
            } catch (Exception ex) {
                postError(callback, "Network error clocking out: " + ex.getMessage());
            }
        });
    }

    public void getMyTasks(String token, Models.ApiCallback<List<Models.TaskItem>> callback) {
        executor.execute(() -> {
            try {
                HttpResult res = makeRequest("GET", "/api/v1/tasks", token, null);
                if (res.statusCode >= 200 && res.statusCode < 300) {
                    JSONArray arr = new JSONArray(res.body);
                    List<Models.TaskItem> tasks = new ArrayList<>();
                    for (int i = 0; i < arr.length(); i++) {
                        JSONObject obj = arr.getJSONObject(i);
                        Models.TaskItem t = new Models.TaskItem();
                        t.id = obj.optString("id");
                        t.title = obj.optString("title", "Untitled Task");
                        t.description = obj.optString("description", "");
                        t.priority = obj.optString("priority", "NORMAL");
                        t.status = obj.optString("status", "PENDING");
                        t.dueDate = obj.optString("dueDate", "");
                        t.deliverableRequired = obj.optBoolean("deliverableGateRequired", false);
                        tasks.add(t);
                    }
                    postSuccess(callback, tasks);
                } else {
                    postError(callback, "Failed to load tasks: HTTP " + res.statusCode);
                }
            } catch (Exception ex) {
                postError(callback, "Error loading tasks: " + ex.getMessage());
            }
        });
    }

    public void updateTaskStatus(String token, String taskId, String newStatus, Models.ApiCallback<Boolean> callback) {
        executor.execute(() -> {
            try {
                JSONObject payload = new JSONObject();
                payload.put("status", newStatus);
                payload.put("deliverableProof", "Updated via Mobile Attendance Desk");

                HttpResult res = makeRequest("PATCH", "/api/v1/tasks/" + taskId + "/status", token, payload.toString());
                if (res.statusCode >= 200 && res.statusCode < 300) {
                    postSuccess(callback, true);
                } else {
                    postError(callback, parseErrorMessage(res.body, "Failed to update task status"));
                }
            } catch (Exception ex) {
                postError(callback, "Error updating task: " + ex.getMessage());
            }
        });
    }

    private HttpResult makeRequest(String method, String endpoint, String token, String jsonBody) throws Exception {
        URL url = new URL(baseUrl + endpoint);
        HttpURLConnection conn = (HttpURLConnection) url.openConnection();
        conn.setRequestMethod(method);
        conn.setConnectTimeout(15000);
        conn.setReadTimeout(15000);
        conn.setRequestProperty("Accept", "application/json");

        if (token != null && !token.isEmpty()) {
            conn.setRequestProperty("Authorization", "Bearer " + token);
        }

        if (jsonBody != null) {
            conn.setDoOutput(true);
            conn.setRequestProperty("Content-Type", "application/json; charset=UTF-8");
            try (OutputStream os = conn.getOutputStream()) {
                os.write(jsonBody.getBytes(StandardCharsets.UTF_8));
                os.flush();
            }
        }

        int code = conn.getResponseCode();
        InputStream is = (code >= 200 && code < 400) ? conn.getInputStream() : conn.getErrorStream();
        StringBuilder sb = new StringBuilder();
        if (is != null) {
            try (BufferedReader reader = new BufferedReader(new InputStreamReader(is, StandardCharsets.UTF_8))) {
                String line;
                while ((line = reader.readLine()) != null) {
                    sb.append(line);
                }
            }
        }

        return new HttpResult(code, sb.toString());
    }

    private String parseErrorMessage(String body, String fallback) {
        try {
            JSONObject json = new JSONObject(body);
            if (json.has("message")) return json.getString("message");
            if (json.has("error")) return json.getString("error");
        } catch (Exception ignored) { }
        return fallback;
    }

    private <T> void postSuccess(Models.ApiCallback<T> cb, T res) {
        mainHandler.post(() -> cb.onSuccess(res));
    }

    private <T> void postError(Models.ApiCallback<T> cb, String err) {
        mainHandler.post(() -> cb.onError(err));
    }

    private static class HttpResult {
        final int statusCode;
        final String body;

        HttpResult(int statusCode, String body) {
            this.statusCode = statusCode;
            this.body = body;
        }
    }
}
