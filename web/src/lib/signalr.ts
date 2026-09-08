import * as signalR from '@microsoft/signalr';
import { LiveWorkstationEvent, WorkstationStatus } from './types';

const API_BASE = process.env.NEXT_PUBLIC_API_URL !== undefined && process.env.NEXT_PUBLIC_API_URL !== '' 
  ? process.env.NEXT_PUBLIC_API_URL 
  : (typeof window !== 'undefined' ? '' : 'http://localhost:5020');


class SignalRManager {
  private connection: signalR.HubConnection | null = null;
  private isConnecting = false;

  public onEventReceived?: (event: LiveWorkstationEvent) => void;
  public onStatusUpdated?: (status: WorkstationStatus) => void;
  public onTaskUpdated?: () => void;
  public onLeaveUpdated?: () => void;
  public onConnectionStateChange?: (connected: boolean) => void;

  public async connect(): Promise<void> {
    if (this.connection && this.connection.state === signalR.HubConnectionState.Connected) {
      return;
    }

    if (this.isConnecting) return;
    this.isConnecting = true;

    try {
      this.connection = new signalR.HubConnectionBuilder()
        .withUrl(`${API_BASE}/hubs/workstation`)
        .withAutomaticReconnect([0, 2000, 5000, 15000, 30000])
        .configureLogging(signalR.LogLevel.Warning)
        .build();

      this.connection.on('WorkstationEventReceived', (evt: LiveWorkstationEvent) => {
        if (this.onEventReceived) this.onEventReceived(evt);
      });

      this.connection.on('WorkstationStatusUpdated', (status: WorkstationStatus) => {
        if (this.onStatusUpdated) this.onStatusUpdated(status);
      });

      this.connection.on('TaskAssigned', () => {
        if (this.onTaskUpdated) this.onTaskUpdated();
      });

      this.connection.on('TaskStatusChanged', () => {
        if (this.onTaskUpdated) this.onTaskUpdated();
      });

      this.connection.on('LeaveUpdated', () => {
        if (this.onLeaveUpdated) this.onLeaveUpdated();
      });

      this.connection.onreconnecting(() => {
        if (this.onConnectionStateChange) this.onConnectionStateChange(false);
      });

      this.connection.onreconnected(async () => {
        if (this.onConnectionStateChange) this.onConnectionStateChange(true);
        await this.joinManagerFeed();
      });

      this.connection.onclose(() => {
        if (this.onConnectionStateChange) this.onConnectionStateChange(false);
      });

      await this.connection.start();
      await this.joinManagerFeed();

      if (this.onConnectionStateChange) this.onConnectionStateChange(true);
    } catch (err) {
      console.error('SignalR connection error:', err);
      if (this.onConnectionStateChange) this.onConnectionStateChange(false);
    } finally {
      this.isConnecting = false;
    }
  }

  private async joinManagerFeed() {
    if (this.connection && this.connection.state === signalR.HubConnectionState.Connected) {
      try {
        await this.connection.invoke('JoinManagerFeed');
      } catch (err) {
        console.error('JoinManagerFeed error:', err);
      }
    }
  }

  public async disconnect(): Promise<void> {
    if (this.connection) {
      await this.connection.stop();
      this.connection = null;
    }
  }
}

export const signalRClient = new SignalRManager();
