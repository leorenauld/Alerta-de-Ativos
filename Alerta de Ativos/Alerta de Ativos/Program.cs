using System.Configuration;
using System.Collections.Specialized;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Mail;
using System.Web.Script.Serialization;


namespace ConsoleApp1
{
    class Program
    {
        // configuração de envio de email
        static string Sender = ConfigurationManager.AppSettings.Get("Sender");
        static string Recipient = ConfigurationManager.AppSettings.Get("Recipient");
        static string clientName = ConfigurationManager.AppSettings.Get("clientName");
        static int port = Convert.ToInt32(ConfigurationManager.AppSettings.Get("port"));
        static string user = ConfigurationManager.AppSettings.Get("user");
        static string password = ConfigurationManager.AppSettings.Get("password");
        static SmtpClient Client = new SmtpClient(clientName);
        // variaveis para recepção do input
        static decimal firstValue;
        static decimal lowValue;
        static decimal highValue;
        static string Symbol;
        // configuração para uso do arquivo de configuração para criação do email
        static string Subject = ConfigurationManager.AppSettings.Get("Subject");
        static string SellEmailBody = ConfigurationManager.AppSettings.Get("SellEmailBody");
        static string BuyEmailBody = ConfigurationManager.AppSettings.Get("BuyEmailBody");
        // intervalo em minutos entre as operações (recomendado o minimo de 3 minutos, devido aos limites da api)
        static int intervals = 3;
        // intervalo em minutos usado pra calcular o aumento ou queda percentual do ativo
        static int historyIntervals = 4;

        static void UserInput()
        {
            Console.WriteLine("Gerenciador de Ativo por preço");
            Console.WriteLine("------------------------------\n");

            Console.WriteLine("Digite o simbolo da Empresa e pressione enter (Diferencie maiusculas de minusculas)");
            Symbol = Console.ReadLine();
            firstValue = Api();
            if (firstValue == 0)
            {
                UserInput();
            }
            else
            {
                Console.WriteLine("Digite o valor para alerta de compra (decimais separados por vírgula (,) )");
                lowValue = Convert.ToDecimal(Console.ReadLine());
                Console.WriteLine("Digite o valor para o alerta de venda (decimais separados por vírgula (,) )");
                highValue = Convert.ToDecimal(Console.ReadLine());
            }

        }


        static decimal Api()
        {
            string QUERY_URL = "https://api.hgbrasil.com/finance/stock_price?key=cc13b7ca&symbol=" + Symbol;
            Uri queryUri = new Uri(QUERY_URL);

            using (WebClient client = new WebClient())
            {
                JavaScriptSerializer js = new JavaScriptSerializer();
                dynamic json_data = js.Deserialize(client.DownloadString(queryUri), typeof(object));

                try
                {
                    Console.WriteLine("\nPreço atual do ativo: {0}", json_data["results"][Symbol]["price"]);
                    return (json_data["results"][Symbol]["price"]);
                }
                catch (Exception)
                {
                    Console.WriteLine("Simbolo da Empresa não encontrado, em caso de dúvida esta é uma lista dos simbolos suportados: https://console.hgbrasil.com/documentation/finance/symbols \n");
                    return 0;
                }

            }
        }

        static void Main(string[] args)
        {
            //Recebe o valor do preço
            decimal value;

            // determina qual a ultima posição do preço da ação, 0 - abaixo do preço de compra; 1 - entre a faixa de preço, 2 - acima do preço de venda;
            int state = 1;

            Program.UserInput();

            // Configurações para o envio de e-mail
            Client.UseDefaultCredentials = false;
            Client.Port = port;
            Client.DeliveryMethod = SmtpDeliveryMethod.Network;
            Client.Credentials = new NetworkCredential(user, password);
            Client.EnableSsl = true;

            // Fila do historico de valores para criação de dados percentuais e alerta por mudança de valor
            Queue<decimal> history = new Queue<decimal>();
            decimal historyValue = firstValue;
            decimal historyValuePercent;
            decimal historyPrev;

            if (lowValue >= highValue)
            {
                Console.WriteLine("The values aren't valid");
                Program.UserInput();
            }
            else
            {
                Console.WriteLine("Aplicação iniciada");

                while (true)
                {
                    try
                    {
                        historyPrev = history.Last();
                    }
                    catch
                    {
                        historyPrev = firstValue;
                    }
                    value = Api();
                    history.Enqueue(value);

                    historyValuePercent = Math.Round((100 * value / historyValue) - 100, 2);

                    if (state != 1)
                    {
                        if (Math.Abs(100 - (100 * value / historyPrev)) >= Convert.ToDecimal(0.2))
                        {
                            state = 1;
                        }
                    }

                    if (value <= Program.lowValue && state != 0)
                    {
                        MailMessage email = new MailMessage(
                            Sender,
                            Recipient,
                            "Alerta de preço do ativo: " + Symbol,
                            "O ativo teve uma alteração de " + historyValuePercent + "% nos ultimos " + historyIntervals + " minutos de atividade da aplicação, e está abaixo do preço de compra estabelecido, valendo " + value + " R$ no momento.\nA compra é aconselhada!"
                            );
                        Client.Send(email);

                        state = 0;
                    }
                    if (value >= Program.highValue && state != 2)
                    {
                        MailMessage email = new MailMessage(
                            Sender,
                            Recipient,
                            "Alerta de preço do ativo: " + Symbol,
                            "O ativo teve uma alteração de " + historyValuePercent + "% nos ultimos " + historyIntervals + " minutos de atividade da aplicação, e está acima do preço de venda estabelecido, valendo " + value + " R$ no momento.\nA venda é aconselhada!"
                            );
                        Client.Send(email);

                        state = 2;
                    }
                    if (value <= Program.highValue && value >= Program.lowValue)
                    {
                        if (state != 1)
                        {
                            MailMessage email = new MailMessage(
                            Sender,
                            Recipient,
                            "Alerta de preço do ativo: " + Symbol,
                            "O ativo retornou a faixa de preço estabelecido, valendo " + value + " R$ no momento."
                            );
                            Client.Send(email);
                        }

                        state = 1;

                        if (history.Count() >= historyIntervals / intervals)
                        {
                            historyValue = history.Peek();
                            history.Dequeue();
                        }

                    }

                    System.Threading.Thread.Sleep(intervals * 60000);
                }
            }
        }
    }
}
