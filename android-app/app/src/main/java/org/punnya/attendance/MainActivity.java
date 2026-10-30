package org.punnya.attendance;

import android.Manifest;
import android.app.AlertDialog;
import android.content.pm.PackageManager;
import android.graphics.Color;
import android.location.Location;
import android.os.Build;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.view.LayoutInflater;
import android.view.View;
import android.widget.Button;
import android.widget.EditText;
import android.widget.ImageButton;
import android.widget.LinearLayout;
import android.widget.ProgressBar;
import android.widget.TextView;
import android.widget.Toast;

import androidx.annotation.NonNull;
import androidx.appcompat.app.AppCompatActivity;
import androidx.core.app.ActivityCompat;
import androidx.core.content.ContextCompat;
import androidx.recyclerview.widget.LinearLayoutManager;
import androidx.recyclerview.widget.RecyclerView;
import androidx.swiperefreshlayout.widget.SwipeRefreshLayout;

import com.google.android.material.switchmaterial.SwitchMaterial;

import org.punnya.attendance.api.ApiClient;
import org.punnya.attendance.api.Models;
import org.punnya.attendance.service.PreferencesManager;
import org.punnya.attendance.service.WifiAndLocationDetector;
import org.punnya.attendance.ui.TaskAdapter;

import java.util.ArrayList;
import java.util.List;
import java.util.Locale;

public class MainActivity extends AppCompatActivity implements WifiAndLocationDetector.DetectionListener {

    private static final int PERMISSION_REQ_CODE = 1001;

    private PreferencesManager prefs;
    private ApiClient apiClient;
    private WifiAndLocationDetector detector;

    // Views: Login
    private LinearLayout layoutLogin;
    private EditText editLoginEmail, editLoginPassword;
    private Button btnLogin;
    private ProgressBar progressLogin;
    private TextView txtLoginError;

    // Views: Dashboard
    private SwipeRefreshLayout swipeRefresh;
    private LinearLayout layoutDashboard;
    private TextView txtHeaderUserName, txtHeaderUserEmail;
    private ImageButton btnSettings, btnLogout;
    private SwitchMaterial switchAutoClockIn;
    private View viewWifiDot, viewLocationDot;
    private TextView txtWifiStatus, txtLocationStatus;
    private TextView txtShiftBadge, txtShiftTimer, txtWorkedToday;
    private Button btnClockIn, btnClockOut;
    private TextView txtTaskCount, txtEmptyTasks;
    private RecyclerView recyclerTasks;
    private TaskAdapter taskAdapter;

    // Timer
    private final Handler timerHandler = new Handler(Looper.getMainLooper());
    private Runnable timerRunnable;
    private boolean isClockedIn = false;
    private long clockInEpoch = 0;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_main);

        prefs = new PreferencesManager(this);
        apiClient = new ApiClient(prefs.getApiUrl());
        detector = new WifiAndLocationDetector(this, prefs);
        detector.setListener(this);

        initViews();
        setupListeners();
        checkPermissions();

        if (prefs.isLoggedIn()) {
            showDashboard();
        } else {
            showLogin();
        }
    }

    private void initViews() {
        // Login
        layoutLogin = findViewById(R.id.layoutLogin);
        editLoginEmail = findViewById(R.id.editLoginEmail);
        editLoginPassword = findViewById(R.id.editLoginPassword);
        btnLogin = findViewById(R.id.btnLogin);
        progressLogin = findViewById(R.id.progressLogin);
        txtLoginError = findViewById(R.id.txtLoginError);

        // Dashboard
        swipeRefresh = findViewById(R.id.swipeRefresh);
        layoutDashboard = findViewById(R.id.layoutDashboard);
        txtHeaderUserName = findViewById(R.id.txtHeaderUserName);
        txtHeaderUserEmail = findViewById(R.id.txtHeaderUserEmail);
        btnSettings = findViewById(R.id.btnSettings);
        btnLogout = findViewById(R.id.btnLogout);
        switchAutoClockIn = findViewById(R.id.switchAutoClockIn);
        viewWifiDot = findViewById(R.id.viewWifiDot);
        viewLocationDot = findViewById(R.id.viewLocationDot);
        txtWifiStatus = findViewById(R.id.txtWifiStatus);
        txtLocationStatus = findViewById(R.id.txtLocationStatus);
        txtShiftBadge = findViewById(R.id.txtShiftBadge);
        txtShiftTimer = findViewById(R.id.txtShiftTimer);
        txtWorkedToday = findViewById(R.id.txtWorkedToday);
        btnClockIn = findViewById(R.id.btnClockIn);
        btnClockOut = findViewById(R.id.btnClockOut);
        txtTaskCount = findViewById(R.id.txtTaskCount);
        txtEmptyTasks = findViewById(R.id.txtEmptyTasks);
        recyclerTasks = findViewById(R.id.recyclerTasks);

        recyclerTasks.setLayoutManager(new LinearLayoutManager(this));
        taskAdapter = new TaskAdapter((task, nextStatus) -> {
            updateTaskStatus(task.id, nextStatus);
        });
        recyclerTasks.setAdapter(taskAdapter);

        switchAutoClockIn.setChecked(prefs.isAutoClockInEnabled());
    }

    private void setupListeners() {
        btnLogin.setOnClickListener(v -> attemptLogin());

        btnLogout.setOnClickListener(v -> {
            new AlertDialog.Builder(this)
                .setTitle("Log Out")
                .setMessage("Are you sure you want to log out?")
                .setPositiveButton("Log Out", (d, w) -> {
                    stopShiftTimer();
                    prefs.clearAuth();
                    showLogin();
                })
                .setNegativeButton("Cancel", null)
                .show();
        });

        btnSettings.setOnClickListener(v -> showSettingsDialog());

        switchAutoClockIn.setOnCheckedChangeListener((b, isChecked) -> {
            prefs.setAutoClockInEnabled(isChecked);
            Toast.makeText(this, isChecked ? "Auto clock-in enabled" : "Auto clock-in disabled", Toast.LENGTH_SHORT).show();
        });

        btnClockIn.setOnClickListener(v -> performClockIn(false));

        btnClockOut.setOnClickListener(v -> {
            new AlertDialog.Builder(this)
                .setTitle("Drop-In Clock Out")
                .setMessage("Clock out and conclude your shift for today?")
                .setPositiveButton("Clock Out", (d, w) -> performClockOut())
                .setNegativeButton("Keep Working", null)
                .show();
        });

        swipeRefresh.setOnRefreshListener(this::refreshData);
    }

    private void checkPermissions() {
        List<String> needed = new ArrayList<>();
        if (ContextCompat.checkSelfPermission(this, Manifest.permission.ACCESS_FINE_LOCATION) != PackageManager.PERMISSION_GRANTED) {
            needed.add(Manifest.permission.ACCESS_FINE_LOCATION);
        }
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
            if (ContextCompat.checkSelfPermission(this, Manifest.permission.POST_NOTIFICATIONS) != PackageManager.PERMISSION_GRANTED) {
                needed.add(Manifest.permission.POST_NOTIFICATIONS);
            }
        }
        if (!needed.isEmpty()) {
            ActivityCompat.requestPermissions(this, needed.toArray(new String[0]), PERMISSION_REQ_CODE);
        }
    }

    @Override
    public void onRequestPermissionsResult(int requestCode, @NonNull String[] permissions, @NonNull int[] grantResults) {
        super.onRequestPermissionsResult(requestCode, permissions, grantResults);
        if (requestCode == PERMISSION_REQ_CODE) {
            detector.startMonitoring();
        }
    }

    @Override
    protected void onResume() {
        super.onResume();
        if (prefs.isLoggedIn()) {
            detector.startMonitoring();
            refreshData();
        }
    }

    @Override
    protected void onPause() {
        super.onPause();
        detector.stopMonitoring();
    }

    private void showLogin() {
        stopShiftTimer();
        layoutLogin.setVisibility(View.VISIBLE);
        swipeRefresh.setVisibility(View.GONE);
    }

    private void showDashboard() {
        layoutLogin.setVisibility(View.GONE);
        swipeRefresh.setVisibility(View.VISIBLE);

        txtHeaderUserName.setText(prefs.getUserName());
        txtHeaderUserEmail.setText(prefs.getUserEmail());

        detector.startMonitoring();
        refreshData();
    }

    private void attemptLogin() {
        String email = editLoginEmail.getText().toString().trim();
        String pass = editLoginPassword.getText().toString().trim();

        if (email.isEmpty() || pass.isEmpty()) {
            txtLoginError.setText("Please enter both email and password.");
            txtLoginError.setVisibility(View.VISIBLE);
            return;
        }

        txtLoginError.setVisibility(View.GONE);
        btnLogin.setEnabled(false);
        progressLogin.setVisibility(View.VISIBLE);

        apiClient.login(email, pass, new Models.ApiCallback<Models.LoginResult>() {
            @Override
            public void onSuccess(Models.LoginResult res) {
                btnLogin.setEnabled(true);
                progressLogin.setVisibility(View.GONE);

                prefs.setToken(res.token);
                prefs.setUserEmail(res.email);
                prefs.setUserName(res.fullName != null && !res.fullName.isEmpty() ? res.fullName : res.email);

                showDashboard();
            }

            @Override
            public void onError(String error) {
                btnLogin.setEnabled(true);
                progressLogin.setVisibility(View.GONE);
                txtLoginError.setText(error);
                txtLoginError.setVisibility(View.VISIBLE);
            }
        });
    }

    private void refreshData() {
        if (!prefs.isLoggedIn()) {
            swipeRefresh.setRefreshing(false);
            return;
        }

        swipeRefresh.setRefreshing(true);
        String token = prefs.getToken();

        // 1. Fetch Shift
        apiClient.getCurrentShift(token, new Models.ApiCallback<Models.ShiftInfo>() {
            @Override
            public void onSuccess(Models.ShiftInfo info) {
                updateShiftUI(info);
                swipeRefresh.setRefreshing(false);
            }

            @Override
            public void onError(String error) {
                swipeRefresh.setRefreshing(false);
            }
        });

        // 2. Fetch Tasks
        apiClient.getMyTasks(token, new Models.ApiCallback<List<Models.TaskItem>>() {
            @Override
            public void onSuccess(List<Models.TaskItem> tasks) {
                taskAdapter.setTasks(tasks);
                txtTaskCount.setText(tasks.size() + (tasks.size() == 1 ? " task" : " tasks"));
                txtEmptyTasks.setVisibility(tasks.isEmpty() ? View.VISIBLE : View.GONE);
            }

            @Override
            public void onError(String error) {}
        });

        // 3. Update detector status
        detector.checkCurrentStatus();
    }

    private void updateShiftUI(Models.ShiftInfo info) {
        isClockedIn = info.isClockedIn;
        prefs.setClockedIn(isClockedIn);

        if (isClockedIn) {
            clockInEpoch = info.clockInEpoch > 0 ? info.clockInEpoch : System.currentTimeMillis();
            prefs.setClockInEpoch(clockInEpoch);

            txtShiftBadge.setText("CLOCKED IN (ACTIVE SHIFT)");
            txtShiftBadge.setTextColor(Color.parseColor("#10B981"));
            txtShiftBadge.setBackgroundColor(Color.parseColor("#142E24"));

            btnClockIn.setVisibility(View.GONE);
            btnClockOut.setVisibility(View.VISIBLE);

            startShiftTimer();
        } else {
            stopShiftTimer();
            txtShiftBadge.setText("NOT CLOCKED IN");
            txtShiftBadge.setTextColor(Color.parseColor("#EF4444"));
            txtShiftBadge.setBackgroundColor(Color.parseColor("#2E1418"));
            txtShiftTimer.setText("00:00:00");

            btnClockIn.setVisibility(View.VISIBLE);
            btnClockOut.setVisibility(View.GONE);
        }

        txtWorkedToday.setText(String.format(Locale.getDefault(), "Today: %.1f hrs total (%s)", info.totalHoursToday, info.totalTimeFormatted != null ? info.totalTimeFormatted : "0h 0m"));
    }

    private void startShiftTimer() {
        stopShiftTimer();
        timerRunnable = new Runnable() {
            @Override
            public void run() {
                if (!isClockedIn) return;
                long elapsedMillis = Math.max(0, System.currentTimeMillis() - clockInEpoch);
                long seconds = (elapsedMillis / 1000) % 60;
                long minutes = (elapsedMillis / (1000 * 60)) % 60;
                long hours = (elapsedMillis / (1000 * 60 * 60));

                txtShiftTimer.setText(String.format(Locale.getDefault(), "%02d:%02d:%02d", hours, minutes, seconds));
                timerHandler.postDelayed(this, 1000);
            }
        };
        timerHandler.post(timerRunnable);
    }

    private void stopShiftTimer() {
        if (timerRunnable != null) {
            timerHandler.removeCallbacks(timerRunnable);
            timerRunnable = null;
        }
    }

    private void performClockIn(boolean isAuto) {
        String token = prefs.getToken();
        String currentSsid = detector.getCurrentWifiSSID();
        String stationId = "MOBILE-" + (currentSsid.isEmpty() || currentSsid.equals("Not Connected") ? "APP" : currentSsid);

        btnClockIn.setEnabled(false);
        apiClient.clockIn(token, stationId, new Models.ApiCallback<Models.ShiftInfo>() {
            @Override
            public void onSuccess(Models.ShiftInfo shift) {
                btnClockIn.setEnabled(true);
                updateShiftUI(shift);
                String msg = isAuto ? "✓ Auto-Clocked In via Office Wi-Fi (" + currentSsid + ")" : "✓ Shift Clocked In Successfully!";
                Toast.makeText(MainActivity.this, msg, Toast.LENGTH_LONG).show();
            }

            @Override
            public void onError(String error) {
                btnClockIn.setEnabled(true);
                Toast.makeText(MainActivity.this, "Clock In Error: " + error, Toast.LENGTH_LONG).show();
            }
        });
    }

    private void performClockOut() {
        String token = prefs.getToken();
        btnClockOut.setEnabled(false);

        apiClient.clockOut(token, "MOBILE-DROPOUT", new Models.ApiCallback<Models.ShiftInfo>() {
            @Override
            public void onSuccess(Models.ShiftInfo shift) {
                btnClockOut.setEnabled(true);
                updateShiftUI(shift);
                Toast.makeText(MainActivity.this, "✓ Shift Ended. Great job today!", Toast.LENGTH_LONG).show();
            }

            @Override
            public void onError(String error) {
                btnClockOut.setEnabled(true);
                Toast.makeText(MainActivity.this, "Clock Out Error: " + error, Toast.LENGTH_LONG).show();
            }
        });
    }

    private void updateTaskStatus(String taskId, String status) {
        String token = prefs.getToken();
        apiClient.updateTaskStatus(token, taskId, status, new Models.ApiCallback<Boolean>() {
            @Override
            public void onSuccess(Boolean result) {
                Toast.makeText(MainActivity.this, "✓ Task status updated", Toast.LENGTH_SHORT).show();
                refreshData();
            }

            @Override
            public void onError(String error) {
                Toast.makeText(MainActivity.this, "Failed: " + error, Toast.LENGTH_SHORT).show();
            }
        });
    }

    private void showSettingsDialog() {
        View dialogView = LayoutInflater.from(this).inflate(R.layout.dialog_settings, null);
        EditText editWifi = dialogView.findViewById(R.id.editSettingsWifi);
        EditText editApiUrl = dialogView.findViewById(R.id.editSettingsApiUrl);

        editWifi.setText(prefs.getOfficeWifiSsids());
        editApiUrl.setText(prefs.getApiUrl());

        new AlertDialog.Builder(this)
            .setView(dialogView)
            .setPositiveButton("Save Settings", (d, w) -> {
                String ssids = editWifi.getText().toString().trim();
                String apiUrl = editApiUrl.getText().toString().trim();
                if (!ssids.isEmpty()) prefs.setOfficeWifiSsids(ssids);
                if (!apiUrl.isEmpty()) {
                    prefs.setApiUrl(apiUrl);
                    apiClient = new ApiClient(apiUrl);
                }
                detector.checkCurrentStatus();
                Toast.makeText(this, "Settings saved", Toast.LENGTH_SHORT).show();
            })
            .setNegativeButton("Cancel", null)
            .show();
    }

    // --- WifiAndLocationDetector.DetectionListener Callbacks ---

    @Override
    public void onWifiStatusChanged(String ssid, boolean isOfficeWifi) {
        if (isOfficeWifi) {
            viewWifiDot.setBackgroundTintList(ContextCompat.getColorStateList(this, R.color.status_green));
            txtWifiStatus.setText("Wi-Fi: " + ssid + " (Office Network ✓)");
            txtWifiStatus.setTextColor(ContextCompat.getColor(this, R.color.status_green));
        } else {
            viewWifiDot.setBackgroundTintList(ContextCompat.getColorStateList(this, R.color.status_amber));
            txtWifiStatus.setText("Wi-Fi: " + ssid + " (Non-Office)");
            txtWifiStatus.setTextColor(ContextCompat.getColor(this, R.color.text_primary));
        }
    }

    @Override
    public void onLocationUpdated(Location location, double distanceMeters, boolean insideGeofence) {
        if (distanceMeters < 0) {
            viewLocationDot.setBackgroundTintList(ContextCompat.getColorStateList(this, R.color.status_red));
            txtLocationStatus.setText("Location: Mock / Fake GPS Detected (Rejected)");
            txtLocationStatus.setTextColor(ContextCompat.getColor(this, R.color.status_red));
        } else if (insideGeofence) {
            viewLocationDot.setBackgroundTintList(ContextCompat.getColorStateList(this, R.color.status_green));
            txtLocationStatus.setText(String.format(Locale.getDefault(), "Location: Inside Office (~%.0fm)", distanceMeters));
            txtLocationStatus.setTextColor(ContextCompat.getColor(this, R.color.status_green));
        } else {
            viewLocationDot.setBackgroundTintList(ContextCompat.getColorStateList(this, R.color.status_amber));
            txtLocationStatus.setText(String.format(Locale.getDefault(), "Location: Outside (~%.0fm away)", distanceMeters));
            txtLocationStatus.setTextColor(ContextCompat.getColor(this, R.color.text_secondary));
        }
    }

    @Override
    public void onOfficeWifiAutoClockInTriggered(String ssid) {
        if (!isClockedIn && prefs.isLoggedIn() && prefs.isAutoClockInEnabled()) {
            performClockIn(true);
        }
    }
}
