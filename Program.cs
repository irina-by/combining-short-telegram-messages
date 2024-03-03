using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

class Program
{
    private static TelegramBotClient botClient;
    private static Dictionary<long, List<Message>> userMessages = new Dictionary<long, List<Message>>(); //Этот словарь используется для хранения сообщений, отправленных пользователями.
    private static Dictionary<long, Timer> timers = new Dictionary<long, Timer>(); //Этот словарь используется для хранения таймеров, которые отслеживают время ожидания перед обработкой сообщений пользователя

    static async Task Main()
    {
        botClient = new TelegramBotClient("7107188206:AAFPehL0giFpf1l43unWdMcZJlSrKE0aZxc"); // Создание экземпляра TelegramBotClient с токеном

        using CancellationTokenSource cts = new CancellationTokenSource(); // источник отмены токина, который сработает в случае необходимости

        ReceiverOptions receiverOptions = new ReceiverOptions //предоставляет опции для настройки приема обновлений от Telegram
        {
            AllowedUpdates = Array.Empty<UpdateType>() //позволяет получать все типы обновлений от Telegram
        };

        // Этот код запускает процесс получения обновлений от Telegram Bot API с помощью метода StartReceiving у объекта botClient.
        // При получении каждого обновления будет вызываться асинхронный обработчик HandleUpdateAsync, который будет обрабатывать обновление.
        // Если произойдет ошибка при опросе обновлений, будет вызываться асинхронный обработчик HandlePollingErrorAsync.

        botClient.StartReceiving(
            updateHandler: async (client, update, cancellationToken) =>
            {
                await HandleUpdateAsync(client, new Update[] { update }, cancellationToken);
            },
            pollingErrorHandler: async (client, exception, cancellationToken) => await HandlePollingErrorAsync(client, exception, cancellationToken),
            receiverOptions: receiverOptions,
            cancellationToken: cts.Token
        );

        var me = await botClient.GetMeAsync(); //Этот метод отправляет запрос к API Telegram для получения информации о боте, получает имя пользователя(Username) в переменную me

        Console.WriteLine($"Bot started listening to @{me.Username}");  //Эта строка кода выводит в консоль сообщение с именем пользователя бота, чтобы показать, что бот успешно подключился и начал прослушивать входящие сообщения.
        Console.ReadLine(); //Этот код ожидает ввод строки с клавиатуры, что позволяет боту оставаться активным и продолжать прослушивать входящие сообщения, пока не будет введена строка.

        cts.Cancel();//отменяет операцию прослушивания бота
    }


    // Представляет обработчик обновлений чата в Telegram боте
    static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update[] updates, CancellationToken cancellationToken)
    {
        foreach (var update in updates)
        {
            // Проверяем, содержит ли обновление текстовое сообщение
            if (update.Message is not { } message)
                continue;

            // Проверяем, содержит ли сообщение текст
            if (message.Text is not { } messageText)
                continue;

            // Получаем идентификатор чата, в котором было получено сообщение
            var chatId = message.Chat.Id;

            // Выводим информацию о полученном сообщении в консоль
            Console.WriteLine($"Received message '{messageText}' in chat {chatId}");

            // Проверяем, содержит ли словарь userMessages запись с данным chatId.
            // Если нет, то добавляем новую запись в словарь с пустым списком сообщений для данного чата
            if (!userMessages.ContainsKey(chatId))
            {
                userMessages[chatId] = new List<Message>();
            }

            // Добавляем полученное сообщение в список сообщений для данного чата
            userMessages[chatId].Add(message);

            // Проверяем, содержит ли словарь timers запись с данным chatId.
            // Если нет, то создаем новый таймер (Timer) с интервалом 5 секунд и бесконечным временем выполнения (Timeout.InfiniteTimeSpan) и добавляем его в словарь
            if (!timers.ContainsKey(chatId))
            {
                var timer = new Timer(ProcessMessages, chatId, TimeSpan.FromSeconds(5), Timeout.InfiniteTimeSpan);
                timers[chatId] = timer;
            }
            else
            {
                // Если ключ уже присутствует в словаре timers,
                // то изменяем интервал выполнения снова на 5 секунд
                timers[chatId].Change(TimeSpan.FromSeconds(5), Timeout.InfiniteTimeSpan);
            }
        }
    }

    private static void ProcessMessages(object state)
    {
        var chatId = (long)state;  // Получаем идентификатор чата из состояния (state)

        if (userMessages.ContainsKey(chatId) && userMessages[chatId].Count > 2)
        {
            var combinedMessage = CombineMessages(userMessages[chatId]);  // Объединяем сообщения из списка userMessages[chatId] в одно сообщение

            foreach (var message in userMessages[chatId])
            {
                DeleteMessage(chatId, message.MessageId);  // Удаляем каждое сообщение из списка userMessages[chatId]
            }

            SendMessage(chatId, combinedMessage);  // Отправляем объединенное сообщение в чат с идентификатором chatId
        }

        userMessages[chatId].Clear();  // Очищаем список сообщений userMessages[chatId]
        timers[chatId].Dispose();  // Освобождаем ресурсы, занятые таймером timers[chatId]
        timers.Remove(chatId);  // Удаляем таймер timers[chatId] из словаря timers
    }

    private static string CombineMessages(List<Message> messages) // Вид объединения сообщений
    {
        // Словарь для хранения StringBuilder для каждого пользователя
        Dictionary<string, StringBuilder> userMessages = new Dictionary<string, StringBuilder>();

        // Перебираем сообщения и добавляем их в соответствующий StringBuilder в словаре
        foreach (Message message in messages)
        {
            // Проверяем, что текст сообщения не пустой и его длина находится в допустимом диапазоне
            if (!string.IsNullOrEmpty(message.Text) && message.Text.Length >= 1 && message.Text.Length <= 20)
            {
                // Получаем имя пользователя
                string username = message.From.Username;

                // Если в словаре нет StringBuilder для данного пользователя, создаем его
                if (!userMessages.ContainsKey(username))
                {
                    userMessages[username] = new StringBuilder();
                }

                // Добавляем текст сообщения в StringBuilder для данного пользователя
                userMessages[username].Append($" {message.Text}");
            }
        }

        // Объединяем StringBuilder для каждого пользователя
        StringBuilder combinedMessage = new StringBuilder();

        foreach (KeyValuePair<string, StringBuilder> userMessage in userMessages)
        {
            // Получаем имя пользователя и соответствующий StringBuilder
            string username = userMessage.Key;
            StringBuilder messageBuilder = userMessage.Value;

            // Добавляем объединенное сообщение в окончательное объединенное сообщение
            combinedMessage.AppendLine($"{username}:{messageBuilder.ToString()}");
        }

        // Возвращаем объединенное сообщение в виде строки
        return combinedMessage.ToString();
    }

    private static async void SendMessage(long chatId, string text) //метод отправления сообщения
    {
        if (!string.IsNullOrEmpty(text))  // Проверяем, не является ли строка text пустой или null
        {
            try
            {
                await botClient.SendTextMessageAsync(chatId, text);  // Отправляем текстовое сообщение в чат с идентификатором chatId
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to send message: {ex.Message}");  // В случае ошибки выводим сообщение об ошибке в консоль
            }
        }
    }

    private static async void DeleteMessage(long chatId, int messageId)
    {
        try
        {
            await botClient.DeleteMessageAsync(chatId, messageId);  // Удаляем сообщение с заданным идентификатором из чата с заданным идентификатором
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to delete message: {ex.Message}");  // В случае ошибки выводим сообщение об ошибке в консоль
        }
    }

    static Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken) //метод обработки ошибок опроса асинхронно
    {
        var errorMessage = exception switch  // Проверяем тип исключения и выбираем соответствующий шаблон сообщения об ошибке
        {
            ApiRequestException apiRequestException
                => $"TelegramAPI Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",  // Если исключение типа ApiRequestException, формируем сообщение об ошибке с информацией об ошибке Telegram API
            _ => exception.ToString()  // Если исключение другого типа, преобразуем его в строку
        };

        Console.WriteLine(errorMessage);  // Выводим сообщение об ошибке в консоль
        return Task.CompletedTask;  // Возвращаем завершенную задачу
    }
}