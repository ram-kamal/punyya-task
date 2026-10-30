package org.punnya.attendance.service;

import android.Manifest;
import android.content.Context;
import android.content.pm.PackageManager;
import android.location.Location;
import android.location.LocationListener;
import android.location.LocationManager;
import android.net.ConnectivityManager;
import android.net.Network;
import android.net.NetworkCapabilities;
import android.net.NetworkRequest;
import android.net.wifi.WifiInfo;
import android.net.wifi.WifiManager;
import android.os.Build;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;

import androidx.core.content.ContextCompat;

import java.util.Locale;

public class WifiAndLocationDetector {

    public interface DetectionListener {
        void onWifiStatusChanged(String ssid, boolean isOfficeWifi);
        void onLocationUpdated(Location location, double distanceMeters, boolean insideGeofence);
        void onOfficeWifiAutoClockInTriggered(String ssid);
    }

    private final Context context;
    private final PreferencesManager prefs;
    private final Handler mainHandler = new Handler(Looper.getMainLooper());
    private DetectionListener listener;
    private ConnectivityManager.NetworkCallback networkCallback;
    private LocationManager locationManager;
    private LocationListener locationListener;

    public WifiAndLocationDetector(Context context, PreferencesManager prefs) {
        this.context = context.getApplicationContext();
        this.prefs = prefs;
    }

    public void setListener(DetectionListener listener) {
        this.listener = listener;
    }

    public void startMonitoring() {
        startWifiMonitoring();
        startLocationUpdates();
        checkCurrentStatus();
    }

    public void stopMonitoring() {
        stopWifiMonitoring();
        stopLocationUpdates();
    }

    public void checkCurrentStatus() {
        String ssid = getCurrentWifiSSID();
        boolean isOffice = isOfficeWifi(ssid);
        if (listener != null) {
            listener.onWifiStatusChanged(ssid, isOffice);
        }

        Location loc = getLastKnownLocation();
        if (loc != null) {
            evaluateLocation(loc);
        }
    }

    public String getCurrentWifiSSID() {
        try {
            ConnectivityManager cm = (ConnectivityManager) context.getSystemService(Context.CONNECTIVITY_SERVICE);
            if (cm != null) {
                Network activeNet = cm.getActiveNetwork();
                if (activeNet != null) {
                    NetworkCapabilities caps = cm.getNetworkCapabilities(activeNet);
                    if (caps != null && caps.hasTransport(NetworkCapabilities.TRANSPORT_WIFI)) {
                        WifiManager wm = (WifiManager) context.getSystemService(Context.WIFI_SERVICE);
                        if (wm != null) {
                            WifiInfo info = wm.getConnectionInfo();
                            if (info != null && info.getSSID() != null) {
                                String raw = info.getSSID().trim();
                                if (raw.startsWith("\"") && raw.endsWith("\"") && raw.length() >= 2) {
                                    raw = raw.substring(1, raw.length() - 1);
                                }
                                if (!raw.equals("<unknown ssid>") && !raw.isEmpty()) {
                                    return raw;
                                }
                            }
                        }
                        return "Connected to Wi-Fi";
                    }
                }
            }
        } catch (Exception ignored) { }
        return "Not Connected";
    }

    public boolean isOfficeWifi(String currentSsid) {
        if (currentSsid == null || currentSsid.isEmpty() || currentSsid.equalsIgnoreCase("Not Connected")) {
            return false;
        }

        String allowed = prefs.getOfficeWifiSsids();
        if (allowed == null || allowed.isEmpty()) return false;

        String[] parts = allowed.split(",");
        for (String p : parts) {
            String trimmed = p.trim();
            if (!trimmed.isEmpty() && currentSsid.equalsIgnoreCase(trimmed)) {
                return true;
            }
        }
        return false;
    }

    private void startWifiMonitoring() {
        try {
            ConnectivityManager cm = (ConnectivityManager) context.getSystemService(Context.CONNECTIVITY_SERVICE);
            if (cm == null) return;

            NetworkRequest request = new NetworkRequest.Builder()
                .addTransportType(NetworkCapabilities.TRANSPORT_WIFI)
                .build();

            networkCallback = new ConnectivityManager.NetworkCallback() {
                @Override
                public void onAvailable(Network network) {
                    mainHandler.postDelayed(() -> {
                        String ssid = getCurrentWifiSSID();
                        boolean isOffice = isOfficeWifi(ssid);
                        if (listener != null) {
                            listener.onWifiStatusChanged(ssid, isOffice);
                        }

                        if (isOffice && prefs.isAutoClockInEnabled() && prefs.isLoggedIn() && !prefs.isClockedIn()) {
                            if (listener != null) {
                                listener.onOfficeWifiAutoClockInTriggered(ssid);
                            }
                        }
                    }, 1500); // 1.5s delay to allow DHCP and SSID negotiation
                }

                @Override
                public void onLost(Network network) {
                    mainHandler.post(() -> {
                        if (listener != null) {
                            listener.onWifiStatusChanged("Disconnected", false);
                        }
                    });
                }
            };

            cm.registerNetworkCallback(request, networkCallback);
        } catch (Exception ignored) { }
    }

    private void stopWifiMonitoring() {
        try {
            if (networkCallback != null) {
                ConnectivityManager cm = (ConnectivityManager) context.getSystemService(Context.CONNECTIVITY_SERVICE);
                if (cm != null) {
                    cm.unregisterNetworkCallback(networkCallback);
                }
                networkCallback = null;
            }
        } catch (Exception ignored) { }
    }

    private void startLocationUpdates() {
        if (ContextCompat.checkSelfPermission(context, Manifest.permission.ACCESS_FINE_LOCATION) != PackageManager.PERMISSION_GRANTED) {
            return;
        }

        try {
            locationManager = (LocationManager) context.getSystemService(Context.LOCATION_SERVICE);
            if (locationManager == null) return;

            locationListener = new LocationListener() {
                @Override
                public void onLocationChanged(Location location) {
                    evaluateLocation(location);
                }

                @Override
                public void onStatusChanged(String provider, int status, Bundle extras) {}
                @Override
                public void onProviderEnabled(String provider) {}
                @Override
                public void onProviderDisabled(String provider) {}
            };

            if (locationManager.isProviderEnabled(LocationManager.GPS_PROVIDER)) {
                locationManager.requestLocationUpdates(LocationManager.GPS_PROVIDER, 15000, 10, locationListener, Looper.getMainLooper());
            } else if (locationManager.isProviderEnabled(LocationManager.NETWORK_PROVIDER)) {
                locationManager.requestLocationUpdates(LocationManager.NETWORK_PROVIDER, 15000, 10, locationListener, Looper.getMainLooper());
            }
        } catch (Exception ignored) { }
    }

    private void stopLocationUpdates() {
        try {
            if (locationManager != null && locationListener != null) {
                locationManager.removeUpdates(locationListener);
                locationListener = null;
            }
        } catch (Exception ignored) { }
    }

    public Location getLastKnownLocation() {
        if (ContextCompat.checkSelfPermission(context, Manifest.permission.ACCESS_FINE_LOCATION) != PackageManager.PERMISSION_GRANTED) {
            return null;
        }

        try {
            LocationManager lm = (LocationManager) context.getSystemService(Context.LOCATION_SERVICE);
            if (lm == null) return null;

            Location gps = lm.getLastKnownLocation(LocationManager.GPS_PROVIDER);
            Location net = lm.getLastKnownLocation(LocationManager.NETWORK_PROVIDER);

            if (gps != null && net != null) {
                return (gps.getTime() > net.getTime()) ? gps : net;
            }
            return (gps != null) ? gps : net;
        } catch (Exception ignored) {
            return null;
        }
    }

    private void evaluateLocation(Location loc) {
        if (loc == null) return;

        // Anti-spoofing check
        boolean isMock = false;
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.S) {
            isMock = loc.isMock();
        } else {
            isMock = loc.isFromMockProvider();
        }

        if (isMock) {
            // Flagged as mock/fake GPS
            if (listener != null) {
                listener.onLocationUpdated(loc, -1, false);
            }
            return;
        }

        double officeLat = prefs.getOfficeLatitude();
        double officeLng = prefs.getOfficeLongitude();
        int radius = prefs.getOfficeRadiusMeters();

        float[] results = new float[1];
        Location.distanceBetween(loc.getLatitude(), loc.getLongitude(), officeLat, officeLng, results);
        double distance = results[0];
        boolean inside = distance <= radius;

        if (listener != null) {
            listener.onLocationUpdated(loc, distance, inside);
        }
    }
}
