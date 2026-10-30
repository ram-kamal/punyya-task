package org.punnya.attendance.service;

import android.content.Context;
import android.content.SharedPreferences;

public class PreferencesManager {
    private static final String PREF_NAME = "phf_attendance_prefs";
    private static final String KEY_TOKEN = "auth_token";
    private static final String KEY_USER_ID = "user_id";
    private static final String KEY_USER_EMAIL = "user_email";
    private static final String KEY_USER_NAME = "user_name";
    private static final String KEY_USER_ROLE = "user_role";
    private static final String KEY_OFFICE_SSIDS = "office_wifi_ssids";
    private static final String KEY_OFFICE_LAT = "office_latitude";
    private static final String KEY_OFFICE_LNG = "office_longitude";
    private static final String KEY_OFFICE_RADIUS = "office_radius_meters";
    private static final String KEY_AUTO_CLOCK_IN = "auto_clock_in_enabled";
    private static final String KEY_IS_CLOCKED_IN = "is_clocked_in";
    private static final String KEY_CLOCK_IN_EPOCH = "clock_in_epoch";
    private static final String KEY_API_URL = "api_url";

    // Default configuration
    public static final String DEFAULT_API_URL = "https://punnya-api.onrender.com";
    public static final String DEFAULT_OFFICE_SSIDS = "Saaol_Office, PHF-Staff, Punnya-Staff, Saaol-Internal, Saaol-Guest";
    public static final double DEFAULT_OFFICE_LAT = 28.5355; // Default reference coords
    public static final double DEFAULT_OFFICE_LNG = 77.2510;
    public static final int DEFAULT_OFFICE_RADIUS = 100; // 100 meters

    private final SharedPreferences prefs;

    public PreferencesManager(Context context) {
        this.prefs = context.getSharedPreferences(PREF_NAME, Context.MODE_PRIVATE);
    }

    public String getToken() {
        return prefs.getString(KEY_TOKEN, null);
    }

    public void setToken(String token) {
        prefs.edit().putString(KEY_TOKEN, token).apply();
    }

    public void clearAuth() {
        prefs.edit()
            .remove(KEY_TOKEN)
            .remove(KEY_USER_ID)
            .remove(KEY_USER_EMAIL)
            .remove(KEY_USER_NAME)
            .remove(KEY_USER_ROLE)
            .remove(KEY_IS_CLOCKED_IN)
            .remove(KEY_CLOCK_IN_EPOCH)
            .apply();
    }

    public boolean isLoggedIn() {
        return getToken() != null && !getToken().trim().isEmpty();
    }

    public String getUserEmail() {
        return prefs.getString(KEY_USER_EMAIL, "");
    }

    public void setUserEmail(String email) {
        prefs.edit().putString(KEY_USER_EMAIL, email).apply();
    }

    public String getUserName() {
        return prefs.getString(KEY_USER_NAME, "Staff Member");
    }

    public void setUserName(String name) {
        prefs.edit().putString(KEY_USER_NAME, name).apply();
    }

    public String getOfficeWifiSsids() {
        return prefs.getString(KEY_OFFICE_SSIDS, DEFAULT_OFFICE_SSIDS);
    }

    public void setOfficeWifiSsids(String ssids) {
        prefs.edit().putString(KEY_OFFICE_SSIDS, ssids).apply();
    }

    public double getOfficeLatitude() {
        return Double.parseDouble(prefs.getString(KEY_OFFICE_LAT, String.valueOf(DEFAULT_OFFICE_LAT)));
    }

    public void setOfficeLatitude(double lat) {
        prefs.edit().putString(KEY_OFFICE_LAT, String.valueOf(lat)).apply();
    }

    public double getOfficeLongitude() {
        return Double.parseDouble(prefs.getString(KEY_OFFICE_LNG, String.valueOf(DEFAULT_OFFICE_LNG)));
    }

    public void setOfficeLongitude(double lng) {
        prefs.edit().putString(KEY_OFFICE_LNG, String.valueOf(lng)).apply();
    }

    public int getOfficeRadiusMeters() {
        return prefs.getInt(KEY_OFFICE_RADIUS, DEFAULT_OFFICE_RADIUS);
    }

    public void setOfficeRadiusMeters(int radius) {
        prefs.edit().putInt(KEY_OFFICE_RADIUS, radius).apply();
    }

    public boolean isAutoClockInEnabled() {
        return prefs.getBoolean(KEY_AUTO_CLOCK_IN, true);
    }

    public void setAutoClockInEnabled(boolean enabled) {
        prefs.edit().putBoolean(KEY_AUTO_CLOCK_IN, enabled).apply();
    }

    public boolean isClockedIn() {
        return prefs.getBoolean(KEY_IS_CLOCKED_IN, false);
    }

    public void setClockedIn(boolean clockedIn) {
        prefs.edit().putBoolean(KEY_IS_CLOCKED_IN, clockedIn).apply();
    }

    public long getClockInEpoch() {
        return prefs.getLong(KEY_CLOCK_IN_EPOCH, 0);
    }

    public void setClockInEpoch(long epochMillis) {
        prefs.edit().putLong(KEY_CLOCK_IN_EPOCH, epochMillis).apply();
    }

    public String getApiUrl() {
        return prefs.getString(KEY_API_URL, DEFAULT_API_URL);
    }

    public void setApiUrl(String url) {
        prefs.edit().putString(KEY_API_URL, url).apply();
    }
}
