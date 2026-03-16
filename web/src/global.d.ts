export {};

declare global {
    interface External {
        receiveMessage(callback: (json: string) => void): void;
        sendMessage(message: string): void;
    }
}
