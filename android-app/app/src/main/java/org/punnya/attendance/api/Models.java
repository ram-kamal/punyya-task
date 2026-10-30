package org.punnya.attendance.api;

public class Models {

    public interface ApiCallback<T> {
        void onSuccess(T result);
        void onError(String error);
    }

    public static class LoginResult {
        public boolean success;
        public String token;
        public String userId;
        public String email;
        public String fullName;
        public String role;
        public String error;
    }

    public static class ShiftInfo {
        public boolean isClockedIn;
        public String status;
        public long clockInEpoch;
        public double totalHoursToday;
        public String totalTimeFormatted;
        public String activeTimeFormatted;
        public String stationId;
        public String error;
    }

    public static class TaskItem {
        public String id;
        public String title;
        public String description;
        public String priority; // URGENT, HIGH, NORMAL, LOW
        public String status;   // PENDING, IN_PROGRESS, DELIVERED, COMPLETED
        public String dueDate;
        public boolean deliverableRequired;
    }
}
