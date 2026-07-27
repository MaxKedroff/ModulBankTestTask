# Candidate Service
Сервис для проведения платёжных операций через внешнего провайдера с гарантией идемпотентности и сохранением состояния при сбоях.

# Технологии
.NET 10.0 — платформа разработки

ASP.NET Core — веб-фреймворк

Entity Framework Core + SQLite — ORM и постоянное хранилище

MediatR — реализация CQRS и Mediator паттерна

Polly — политики повторных попыток с экспоненциальной задержкой и jitter

Serilog — структурированное логирование

AppMetrics — сбор метрик производительности

Docker & Docker Compose — контейнеризация

# Архитектура
Проект построен на Onion Architecture

# Запуск
// Сборка и запуск

docker compose up --build

// Остановка

docker compose down

// Остановка с очисткой данных

docker compose down -v



# Swagger UI
После запуска Swagger доступен по адресу: http://localhost:8080/swagger
