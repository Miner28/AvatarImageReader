from PIL import Image;


def set_bit(value, state, bit):
    if (state):
        return value | (1 << bit)
    else:
        return value & ~(1 << bit)


def read_bit(value, index):
    return (value >> index) & 1

def readInformation():
    img = Image.open("img.png")

    byte_list = bytearray([])
    index = 0
    current_byte = 0
    for y in range(img.height):
        for x in reversed(range(img.width)):
            pixel = img.getpixel((x,y))
            current_byte = set_bit(current_byte,pixel[0] == 255,index)
            index += 1
            current_byte = set_bit(current_byte,pixel[1] == 255,index)
            index += 1
            if index != 8:
                current_byte = set_bit(current_byte,pixel[2] == 255,index)
                index += 1
            else:
                if pixel[2] != 255:
                    byte_list.append(current_byte)
                    current_byte = 0
                    index = 0
                else:
                    return byte_list


    return byte_list





output = readInformation()
for b in output:
    print(b)

print("-----")
output = output.decode('utf-32')

print([output])
print(len(output))