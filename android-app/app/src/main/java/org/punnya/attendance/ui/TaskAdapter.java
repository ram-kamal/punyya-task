package org.punnya.attendance.ui;

import android.graphics.Color;
import android.view.LayoutInflater;
import android.view.View;
import android.view.ViewGroup;
import android.widget.Button;
import android.widget.TextView;

import androidx.annotation.NonNull;
import androidx.recyclerview.widget.RecyclerView;

import org.punnya.attendance.R;
import org.punnya.attendance.api.Models;

import java.util.ArrayList;
import java.util.List;

public class TaskAdapter extends RecyclerView.Adapter<TaskAdapter.TaskViewHolder> {

    public interface TaskActionCallback {
        void onUpdateStatus(Models.TaskItem task, String nextStatus);
    }

    private final List<Models.TaskItem> tasks = new ArrayList<>();
    private final TaskActionCallback callback;

    public TaskAdapter(TaskActionCallback callback) {
        this.callback = callback;
    }

    public void setTasks(List<Models.TaskItem> newTasks) {
        tasks.clear();
        if (newTasks != null) {
            tasks.addAll(newTasks);
        }
        notifyDataSetChanged();
    }

    @NonNull
    @Override
    public TaskViewHolder onCreateViewHolder(@NonNull ViewGroup parent, int viewType) {
        View view = LayoutInflater.from(parent.getContext()).inflate(R.layout.item_task, parent, false);
        return new TaskViewHolder(view);
    }

    @Override
    public void onBindViewHolder(@NonNull TaskViewHolder holder, int position) {
        Models.TaskItem task = tasks.get(position);
        holder.bind(task, callback);
    }

    @Override
    public int getItemCount() {
        return tasks.size();
    }

    static class TaskViewHolder extends RecyclerView.ViewHolder {
        TextView txtPriority, txtStatus, txtTitle, txtDesc, txtDueDate;
        Button btnUpdate;

        TaskViewHolder(@NonNull View itemView) {
            super(itemView);
            txtPriority = itemView.findViewById(R.id.txtTaskPriority);
            txtStatus = itemView.findViewById(R.id.txtTaskStatus);
            txtTitle = itemView.findViewById(R.id.txtTaskTitle);
            txtDesc = itemView.findViewById(R.id.txtTaskDesc);
            txtDueDate = itemView.findViewById(R.id.txtTaskDueDate);
            btnUpdate = itemView.findViewById(R.id.btnUpdateTask);
        }

        void bind(Models.TaskItem task, TaskActionCallback callback) {
            txtTitle.setText(task.title);
            txtDesc.setText(task.description != null && !task.description.isEmpty() ? task.description : "No description provided");

            // Priority badge styling
            String prio = task.priority != null ? task.priority.toUpperCase() : "NORMAL";
            txtPriority.setText(prio);
            if (prio.contains("URGENT")) {
                txtPriority.setTextColor(Color.parseColor("#EF4444"));
                txtPriority.setBackgroundColor(Color.parseColor("#331418"));
            } else if (prio.contains("HIGH")) {
                txtPriority.setTextColor(Color.parseColor("#F59E0B"));
                txtPriority.setBackgroundColor(Color.parseColor("#332414"));
            } else {
                txtPriority.setTextColor(Color.parseColor("#94A3B8"));
                txtPriority.setBackgroundColor(Color.parseColor("#1E2430"));
            }

            // Status styling
            String status = task.status != null ? task.status.toUpperCase() : "PENDING";
            txtStatus.setText(status.replace("_", " "));
            if (status.contains("COMPLETED") || status.contains("DONE")) {
                txtStatus.setTextColor(Color.parseColor("#10B981"));
                txtStatus.setBackgroundColor(Color.parseColor("#142E24"));
                btnUpdate.setVisibility(View.GONE);
            } else if (status.contains("IN_PROGRESS")) {
                txtStatus.setTextColor(Color.parseColor("#38BDF8"));
                txtStatus.setBackgroundColor(Color.parseColor("#142638"));
                btnUpdate.setVisibility(View.VISIBLE);
                btnUpdate.setText("Mark Done ✓");
                btnUpdate.setOnClickListener(v -> callback.onUpdateStatus(task, "COMPLETED"));
            } else {
                txtStatus.setTextColor(Color.parseColor("#F59E0B"));
                txtStatus.setBackgroundColor(Color.parseColor("#2E2414"));
                btnUpdate.setVisibility(View.VISIBLE);
                btnUpdate.setText("Start Task ▶");
                btnUpdate.setOnClickListener(v -> callback.onUpdateStatus(task, "IN_PROGRESS"));
            }

            // Due Date
            if (task.dueDate != null && !task.dueDate.isEmpty()) {
                String due = task.dueDate.length() >= 10 ? task.dueDate.substring(0, 10) : task.dueDate;
                txtDueDate.setText("Due: " + due);
            } else {
                txtDueDate.setText("No due date");
            }
        }
    }
}
