

namespace SEM
{

    using System;
    using System.Collections.Generic;
    using System.Security.Cryptography;
    using System.Text;

    public class ChineseNameGenerator
    {
        private readonly List<(string surname, double cumulative)> _ranges;
        private readonly double _total;

        public ChineseNameGenerator()
        {
            var data = GetSurnameData();

            _ranges = new List<(string, double)>();
            double sum = 0;

            foreach (var item in data)
            {
                sum += item.Weight;
                _ranges.Add((item.Surname, sum));
            }

            _total = sum;
        }

        /// <summary>
        /// 获取显示名称（自动性别：王先生 / 张小姐）
        /// </summary>
        public string GetDisplayName(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
                throw new ArgumentException("phone不能为空");

            var surname = GetSurname(phone);
            var gender = GetGender(phone);

            return gender == Gender.Male
                ? $"{surname}先生"
                : $"{surname}小姐";
        }

        /// <summary>
        /// 获取姓（稳定 + 加权）
        /// </summary>
        private string GetSurname(string phone)
        {
            double r = StableRandom(phone) * _total;

            int left = 0;
            int right = _ranges.Count - 1;

            while (left < right)
            {
                int mid = (left + right) / 2;

                if (r <= _ranges[mid].cumulative)
                    right = mid;
                else
                    left = mid + 1;
            }

            var surname = _ranges[left].surname;

            if (surname == "其他")
            {
                surname = GetOtherSurname(phone);
            }

            return surname;
        }

        /// <summary>
        /// 自动生成性别（稳定）
        /// </summary>
        private Gender GetGender(string phone)
        {
            double r = StableRandom(phone + "_gender");
            return r < 0.5 ? Gender.Male : Gender.Female;
        }

        /// <summary>
        /// 稳定随机（0~1）
        /// </summary>
        private static double StableRandom(string input)
        {
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));

            long value = BitConverter.ToInt64(bytes, 0);

            return Math.Abs(value / (double)long.MaxValue);
        }

        /// <summary>
        /// 处理“其他姓”
        /// </summary>
        private string GetOtherSurname(string phone)
        {
            double r = StableRandom(phone + "_ext");

            var list = OtherSurnames;

            int index = (int)(r * list.Length);

            return list[index];
        }

        /// <summary>
        /// 姓氏数据
        /// </summary>
        private List<SurnameWeight> GetSurnameData()
        {
            return new List<SurnameWeight>
        {
            new("王", 7.4), new("李", 7.2), new("张", 6.8), new("刘", 5.4), new("陈", 4.9),
            new("杨", 3.1), new("黄", 2.2), new("赵", 2.1), new("吴", 2.1), new("周", 2.0),
            new("徐", 1.7), new("孙", 1.6), new("马", 1.5), new("朱", 1.5), new("胡", 1.3),
            new("郭", 1.2), new("何", 1.2), new("高", 1.1), new("林", 1.1), new("罗", 1.0),

            // 复姓
            new("欧阳", 0.05), new("司马", 0.03), new("上官", 0.03), new("诸葛", 0.02),

            // 长尾
            new("其他", 5.0)
        };
        }

        private static readonly string[] OtherSurnames =
        {
        "冷","辛","简","饶","曾","沙","乜","养","鞠","须","丰","巫","乌","藏"
    };

        private enum Gender
        {
            Male,
            Female
        }

        private record SurnameWeight(string Surname, double Weight);
    }

}
