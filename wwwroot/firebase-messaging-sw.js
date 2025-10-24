importScripts("https://www.gstatic.com/firebasejs/12.4.0/firebase-app-compat.js");
importScripts("https://www.gstatic.com/firebasejs/12.4.0/firebase-messaging-compat.js");

firebase.initializeApp({
    apiKey: "AIzaSyAqBjYCCfBZo-pLFnY6oybdV5ru3ge17IE",
    authDomain: "elite-rentals-42256.firebaseapp.com",
    projectId: "elite-rentals-42256",
    storageBucket: "elite-rentals-42256.firebasestorage.app",
    messagingSenderId: "719647668815",
    appId: "1:719647668815:web:4d5a410415c8df2198483a"
});

const messaging = firebase.messaging();

messaging.onBackgroundMessage(function (payload) {
    console.log("📦 Background message:", payload);
    const notificationTitle = payload.notification.title;
    const notificationOptions = {
        body: payload.notification.body,
        icon: "/icon.png" // Optional: replace with your actual icon path
    };

    self.registration.showNotification(notificationTitle, notificationOptions);
});
